---
name: packet-creator
description: Guide for adding a new MikaProtocol packet (PacketId enum entry, MemoryPackable class, and its handler). Use when defining or creating a new network packet between the server and the client.
---

# Packet Creator

How to add a new network packet in this codebase (MikaProtocol + Roslyn source generator).

---

## STOP — confirm these 3 things first

Before writing any code, you MUST have all three. **If any one is unclear, ask the user — do not guess.**

1. **Direction** → decides the prefix and suffix:

   | Direction | Prefix | Suffix | Example |
   |-----------|--------|--------|---------|
   | Client → Server (client sends) | `C_` | `Request` | `C_LoginRequest` |
   | Server → Client (server sends) | `S_` | `Response` | `S_LoginResponse` |

   The prefix always marks the **sender**. A server-initiated push still uses `S_`.

2. **Body composition** — how many fields, each field's type, and whether any field is a
   `List` / collection or a nested object.

3. **Packet name** — the middle part (e.g. `Login`, `AddItem`), used as `<Prefix><Name><Suffix>`.

---

## Steps

### 1. Add a PacketId enum entry
In `Server/MikaProtocol/MikaPacket.cs`, add the next unused `ushort` value to `enum PacketId`:

```csharp
public enum PacketId : ushort
{
    None = 0,
    // ...existing...
    C_AddItemRequest = 7,
    S_UpdateItemResponse = 8,
    C_MyNewRequest = 9,   // <- new, next free value
}
```

### 2. Define the packet class
In the same file, add a `partial class` implementing `IPacket` with the two attributes:

```csharp
[MemoryPackable, Packet(PacketId.C_MyNewRequest)]
public partial class C_MyNewRequest : IPacket
{
    public long   UserId { get; set; }
    public string Name   { get; set; } = "";   // string fields default to ""
}
```

Rules:
- Must be `partial class` and implement `IPacket`.
- Attributes: `[MemoryPackable, Packet(PacketId.<Name>)]`.
- An **empty body is allowed** (see `C_PingRequest`).
- **Nested / List field:** any custom type used inside a packet body (a `List<T>` element
  or a single nested object) must itself be `[MemoryPackable] partial`. MemoryPack refuses
  to serialize a type that lacks the attribute, so this is a compile error otherwise.

```csharp
[MemoryPackable, Packet(PacketId.S_UpdateItemResponse)]
public partial class S_UpdateItemResponse : IPacket
{
    public List<ItemInfo> Items { get; set; } = new();
}
```

### 2b. Reusable "Info" / data types → `PacketInfo.cs`
Nested payload types (names ending in `Info`, or any shared DTO) live in a **separate file**,
`Server/MikaProtocol/PacketInfo.cs`, not in `MikaPacket.cs`. They are **not** packets:
give them `[MemoryPackable]` only — no `[Packet(...)]`, no `IPacket`.

```csharp
// Server/MikaProtocol/PacketInfo.cs
namespace MikaProtocol
{
    [MemoryPackable]
    public partial class ItemInfo
    {
        public int ItemId { get; set; }
        public int Count  { get; set; }
    }
}
```

(The sync script mirrors every `.cs` under `MikaProtocol`, so a new file here reaches Unity too.)

### 3. Write the handler on the RECEIVING side
Handler method name must be `Handle_<PacketClassName>`. The source generator wires it up at build.

- `C_` packet (received by the **server**) → `Server/WSGameServer/Network/ClientPacketHandler.cs`:

```csharp
[PacketHandler]
public static void Handle_C_MyNewRequest(ISession session, C_MyNewRequest req)
{
    // handle, then reply
    session.SendPacket(new S_UpdateItemResponse { /* ... */ });
}
```

- `S_` packet (received by the **client**) → `Server/MikaDummyClient/Network/ServerPacketHandler.cs`
  (and the Unity client handler):

```csharp
[PacketHandler]
public static void Handle_S_UpdateItemResponse(ISession session, S_UpdateItemResponse res)
{
    // ...
}
```

### 4. Send a packet
```csharp
session.SendPacket(new S_UpdateItemResponse { Items = items });
```

### 5. Build & sync
Build `MikaProtocol`. The post-build `sync-protocol-to-unity.ps1` mirrors the definition into
`Assets/Scripts_Server/Protocol`. **Never edit the Unity copy directly** — it is overwritten.

---

## Checklist
- [ ] Direction, body, and name confirmed (asked the user if unsure)
- [ ] `PacketId` enum entry added (next free ushort)
- [ ] `[MemoryPackable, Packet(...)] partial class : IPacket` added
- [ ] Nested/`Info`/DTO types put in `PacketInfo.cs` as `[MemoryPackable] partial` (no `Packet`/`IPacket`)
- [ ] `Handle_<PacketName>` handler added on the receiving side
- [ ] Built so the Unity mirror regenerates
