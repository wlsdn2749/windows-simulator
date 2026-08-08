using System;

namespace MikaDummyClient
{
    /// <summary>
    /// 메뉴 한 항목을 표현하는 단순 액션. 라벨과 실행 델리게이트로 구성된다.
    /// </summary>
    public sealed class ClientAction
    {
        public string Label { get; }
        private readonly Action _execute;

        public ClientAction(string label, Action execute)
        {
            Label = label;
            _execute = execute;
        }

        public void Execute() => _execute();
    }
}
