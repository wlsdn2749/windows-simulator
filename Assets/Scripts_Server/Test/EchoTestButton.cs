#if UNITY_5_3_OR_NEWER

using UnityEngine;
using MikaNetwork; 
using MikaProtocol;

namespace ServerTest
{
    /// <summary>
    /// 에코 패킷 송신 테스트 버튼.
    /// - uGUI Button 의 OnClick 에 SendEcho() 를 연결한다.
    /// - 클릭 시 C_EchoRequest 를 서버로 보내고, 서버의 S_EchoResponse 로 왕복을 확인한다.
    /// </summary>
    public class EchoTestButton : MonoBehaviour
    {
        [SerializeField] private string message = "Hello Server"; // 보낼 메시지(인스펙터에서 수정 가능)

        /// <summary>버튼 OnClick 연결용. 에코 요청 패킷을 전송한다.</summary>
        public void SendEcho()
        {
            NetworkManager.Instance.Send(new C_EchoRequest { Message = message });
            Debug.Log($"[Client] Send Echo: {message}");
        }
    }
}

#endif
