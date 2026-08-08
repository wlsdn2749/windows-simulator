using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace MikaSourceGen
{
    // 빠뜨린 핸들러 진단 (MIKA001)
    // 그 프로젝트의 [PacketHandler]들이 다루는 접두사(C_/S_)로 "수신 측"을 추론하고,
    // 같은 접두사인데 핸들러가 없는 [Packet]에 대해 경고를 낸다.
    public sealed partial class PacketHandlerGenerator
    {
        static readonly DiagnosticDescriptor MissingHandlerRule = new(
            id: "MIKA001",
            title: "패킷 핸들러 없음",
            messageFormat: "수신 패킷 '{0}'에 [PacketHandler]가 없습니다. 핸들러를 작성하세요.",
            category: "MikaNetwork",
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        // 참조 어셈블리(MikaProtocol 등)까지 포함한 전체 [Packet] 타입의 FQN + 선언 위치.
        //
        // 위치를 함께 모으는 이유: 진단을 Location.None으로 내면 컴파일러 출력에 파일·줄이 빠지는데,
        // Unity는 "파일(줄,열): warning ..." 형식만 파싱해 콘솔에 올린다.
        // 그래서 위치 없는 경고는 Unity에서 조용히 사라진다(dotnet build는 그대로 출력한다).
        // Unity에서는 패킷 정의가 소스(Assets/Scripts_Server/Protocol)에 있으므로 실제 위치가 잡힌다.
        static ImmutableArray<PacketRef> GetAllPacketTypeNames(Compilation comp)
        {
            var results = ImmutableArray.CreateBuilder<PacketRef>();

            void Inspect(INamedTypeSymbol t)
            {
                foreach (var a in t.GetAttributes())
                {
                    if (a.AttributeClass?.ToDisplayString() == PacketAttr)
                    {
                        // 소스에 있는 선언만 위치로 쓴다. 참조 어셈블리(메타데이터) 위치는 파일이 아니다.
                        Location? loc = null;
                        foreach (var l in t.Locations)
                        {
                            if (l.IsInSource) { loc = l; break; }
                        }

                        results.Add(new PacketRef(t.ToDisplayString(), loc));
                        break;
                    }
                }

                foreach (var nested in t.GetTypeMembers())
                    Inspect(nested);
            }

            void Visit(INamespaceSymbol ns)
            {
                foreach (var t in ns.GetTypeMembers())
                    Inspect(t);
                foreach (var child in ns.GetNamespaceMembers())
                    Visit(child);
            }

            // System/MemoryPack 등 대형 어셈블리는 제외하고 Mika* 만 스캔한다.
            Visit(comp.Assembly.GlobalNamespace);
            foreach (var refAsm in comp.SourceModule.ReferencedAssemblySymbols)
            {
                if (refAsm.Name.StartsWith("Mika", System.StringComparison.Ordinal))
                    Visit(refAsm.GlobalNamespace);
            }

            return results.ToImmutable();
        }

        static void ReportMissingHandlers(
            SourceProductionContext spc,
            (ImmutableArray<HandlerInfo> Handlers, ImmutableArray<PacketRef> Packets) input)
        {
            // 핸들러가 하나도 없는 프로젝트(MikaProtocol 등)는 측 추론 불가 -> skip
            if (input.Handlers.IsDefaultOrEmpty) return;

            var handled = new HashSet<string>();
            var inboundPrefixes = new HashSet<string>();

            // 패킷 정의가 참조 어셈블리에 있을 때 쓸 대체 위치(기존 핸들러 중 첫 번째).
            Location? fallback = null;

            foreach (var h in input.Handlers)
            {
                handled.Add(h.PacketType);
                var pfx = Prefix(h.PacketType);
                if (pfx != null) inboundPrefixes.Add(pfx);

                if (fallback is null && h.DeclLocation is not null)
                    fallback = h.DeclLocation;
            }

            if (inboundPrefixes.Count == 0) return;

            foreach (var pkt in input.Packets)
            {
                var pfx = Prefix(pkt.Name);
                if (pfx == null || !inboundPrefixes.Contains(pfx)) continue;
                if (handled.Contains(pkt.Name)) continue;

                // 위치는 ① 패킷 선언(Unity: 소스에 있음) ② 기존 핸들러(서버: 패킷이 참조 어셈블리) 순으로 고른다.
                // 둘 다 없으면 Location.None이 되고, 그 경고는 Unity 콘솔에 뜨지 않는다.
                spc.ReportDiagnostic(
                    Diagnostic.Create(MissingHandlerRule, pkt.Location ?? fallback ?? Location.None, SimpleName(pkt.Name)));
            }
        }

        /// <summary>[Packet] 타입 하나의 FQN과 선언 위치. 위치는 소스에 있을 때만 채워진다.</summary>
        private readonly struct PacketRef : System.IEquatable<PacketRef>
        {
            public readonly string Name;
            public readonly Location? Location;

            public PacketRef(string name, Location? location)
            {
                Name = name;
                Location = location;
            }

            public bool Equals(PacketRef other)
                => Name == other.Name && Equals(Location, other.Location);

            public override bool Equals(object? obj) => obj is PacketRef o && Equals(o);

            public override int GetHashCode()
                => Name.GetHashCode() ^ (Location?.GetHashCode() ?? 0);
        }

        // "MikaProtocol.C_EchoRequest" -> "C_EchoRequest"
        static string SimpleName(string fqn)
        {
            var i = fqn.LastIndexOf('.');
            return i < 0 ? fqn : fqn.Substring(i + 1);
        }

        // "MikaProtocol.C_EchoRequest" -> "C_" (언더스코어 없으면 null)
        static string? Prefix(string fqn)
        {
            var name = SimpleName(fqn);
            var u = name.IndexOf('_');
            return u <= 0 ? null : name.Substring(0, u + 1);
        }
    }
}
