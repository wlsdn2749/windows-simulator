using System;
using System.Runtime.InteropServices;

/// <summary>
/// Win32 / DWM 네이티브 API의 P/Invoke 선언 모음.
///
/// ■ P/Invoke 란?
///   C#(관리 코드)에서 운영체제의 C 기반 DLL 함수(비관리 코드)를 직접 호출하는 기능.
///   [DllImport("user32.dll")] 처럼 어느 DLL의 어떤 함수인지 표시하고, C 함수 시그니처를
///   C#으로 그대로 옮겨 적으면 런타임이 호출을 연결해 준다. (using System.Runtime.InteropServices)
///
/// ■ 이 클래스는 "선언"만 담당한다. 실제 호출 순서/조건은 WindowManager 가 가진다.
///   Task Bar Hero 스타일의 창 제어(투명·보더리스·항상위·클릭스루·드래그)에 필요한
///   함수·상수·구조체를 한곳에 모았다.
///
/// ■ 사용 DLL
///   - user32.dll : 창 스타일·위치·메시지·커서 등 일반 윈도우 관리 API
///   - dwmapi.dll : DWM(Desktop Window Manager, 데스크톱 합성기) — 투명/유리 효과 담당
/// </summary>
public static class Win32Native
{
    // ──────────────────────────────────────────────────────────────
    // 상수 : 창 스타일 인덱스 (SetWindowLong / GetWindowLong 의 nIndex 인자)
    //   어떤 스타일 묶음을 읽고 쓸지 가리키는 "주소" 역할. 음수 매직 넘버는 Win32 규약.
    // ──────────────────────────────────────────────────────────────
    public const int GWL_STYLE   = -16; // 기본 윈도우 스타일(GWL_STYLE) 묶음
    public const int GWL_EXSTYLE = -20; // 확장 윈도우 스타일(GWL_EXSTYLE) 묶음

    // ──────────────────────────────────────────────────────────────
    // 상수 : 기본 스타일 플래그 (GWL_STYLE 에 OR/AND 로 켜고 끈다)
    //   값은 비트 플래그(2의 거듭제곱)라 | 로 합치고 & ~ 로 제거한다.
    // ──────────────────────────────────────────────────────────────
    public const uint WS_POPUP       = 0x80000000; // 팝업 창 : 타이틀바·테두리 없는 형태
    public const uint WS_VISIBLE     = 0x10000000; // 창을 화면에 표시
    public const uint WS_CAPTION     = 0x00C00000; // 타이틀바(캡션) — 보더리스 시 제거 대상
    public const uint WS_THICKFRAME  = 0x00040000; // 크기 조절용 두꺼운 테두리 — 제거 대상
    public const uint WS_MINIMIZEBOX = 0x00020000; // 최소화 버튼 — 제거 대상
    public const uint WS_MAXIMIZEBOX = 0x00010000; // 최대화 버튼 — 제거 대상
    public const uint WS_SYSMENU     = 0x00080000; // 좌상단 시스템 메뉴 — 제거 대상

    // ──────────────────────────────────────────────────────────────
    // 상수 : 확장 스타일 플래그 (GWL_EXSTYLE)
    // ──────────────────────────────────────────────────────────────
    public const uint WS_EX_LAYERED     = 0x00080000; // 레이어드 창(합성 대상) — 클릭스루의 전제 플래그
    public const uint WS_EX_TRANSPARENT = 0x00000020; // 입력 통과 : 마우스 메시지를 이 창이 받지 않고
                                                       //   뒤(아래)에 있는 창으로 흘려보냄(클릭 스루의 핵심)

    // ──────────────────────────────────────────────────────────────
    // 상수 : SetWindowPos 의 hWndInsertAfter 인자 (Z순서에서 어디에 삽입할지)
    //   특수 핸들값(-1, -2)을 IntPtr 로 감싼 것.
    // ──────────────────────────────────────────────────────────────
    public static readonly IntPtr HWND_TOPMOST   = new IntPtr(-1); // 항상 다른 창들 위(최상위)
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2); // 최상위 해제(일반 Z순서로 복귀)

    // ──────────────────────────────────────────────────────────────
    // 상수 : SetWindowPos 의 uFlags (어떤 변경을 적용/생략할지)
    // ──────────────────────────────────────────────────────────────
    public const uint SWP_NOSIZE       = 0x0001; // cx,cy(크기) 인자 무시 → 현재 크기 유지
    public const uint SWP_NOMOVE       = 0x0002; // X,Y(위치) 인자 무시 → 현재 위치 유지
    public const uint SWP_NOZORDER     = 0x0004; // hWndInsertAfter 무시 → 현재 Z순서 유지
    public const uint SWP_NOACTIVATE   = 0x0010; // 창을 활성화(포커스)하지 않음
    public const uint SWP_FRAMECHANGED = 0x0020; // 프레임(비클라이언트 영역)을 다시 계산 →
                                                 //   SetWindowLong 으로 바꾼 스타일을 화면에 실제 반영시킴
    public const uint SWP_SHOWWINDOW   = 0x0040; // 창을 보이게 함

    // ──────────────────────────────────────────────────────────────
    // 상수 : 창 드래그용 시스템 메시지
    // ──────────────────────────────────────────────────────────────
    public const int WM_SYSCOMMAND     = 0x0112; // 시스템 명령 메시지(이동/크기/최소화 등)
    public const int SC_MOVE_HTCAPTION = 0xF012; // SC_MOVE(0xF010) | HTCAPTION(0x0002).
                                                 //   "타이틀바를 잡고 창을 옮긴다"는 명령을 OS에 위임하는 트릭.
                                                 //   보더리스라 타이틀바가 없어도 이 메시지로 창을 끌 수 있다.

    // ──────────────────────────────────────────────────────────────
    // 상수 : SetLayeredWindowAttributes 의 dwFlags
    //   ※ 본 데모는 DWM per-pixel 투명을 쓰므로 이 함수를 호출하지 않는다(선언만 보존).
    //     호출하면 DWM 투명이 균일 알파/색상키 모드로 덮여 창이 검게 변할 수 있다.
    // ──────────────────────────────────────────────────────────────
    public const uint LWA_ALPHA    = 0x00000002; // 창 전체에 균일 알파값 적용
    public const uint LWA_COLORKEY = 0x00000001; // 특정 색을 투명색(색상키)으로 처리

    /// <summary>
    /// DWM 프레임 여백 구조체 (DwmExtendFrameIntoClientArea 인자).
    /// 각 변의 값을 -1 로 주면 "프레임(유리 영역)을 창 전체로 확장"하라는 특수 지시가 되어,
    /// 클라이언트 영역에서 알파가 0인 픽셀이 그대로 투명해진다.
    /// [StructLayout(Sequential)] : 필드를 선언 순서대로 메모리에 배치 → C의 MARGINS 구조체와 1:1 호환.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct MARGINS
    {
        public int leftWidth;    // 왼쪽 여백
        public int rightWidth;   // 오른쪽 여백
        public int topHeight;    // 위쪽 여백
        public int bottomHeight; // 아래쪽 여백
    }

    /// <summary>
    /// 창의 화면상 사각 영역 구조체 (GetWindowRect 의 out 인자).
    /// 데스크톱(스크린) 좌표계 기준이며 left/top 이 창의 좌상단.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int left;   // 좌측 X
        public int top;    // 상단 Y
        public int right;  // 우측 X
        public int bottom; // 하단 Y
    }

    /// <summary>
    /// 화면 좌표 점 구조체 (GetCursorPos 의 out 인자).
    /// Win32 데스크톱 좌표는 좌상단이 (0,0)이고 아래로 갈수록 Y가 커진다(Unity와 Y축 반대).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x; // 데스크톱 X (좌상단 0, 오른쪽 +)
        public int y; // 데스크톱 Y (좌상단 0, 아래쪽 +)
    }

    // ──────────────────────────────────────────────────────────────
    // user32.dll — 일반 윈도우 관리 함수
    // ──────────────────────────────────────────────────────────────

    /// <summary>현재 스레드의 활성 창 핸들을 반환. (스플래시 타이밍엔 부정확할 수 있어 폴백용으로만 사용)</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr GetActiveWindow();

    /// <summary>
    /// 창의 스타일 값을 설정(쓰기). nIndex 로 어떤 묶음(GWL_STYLE/GWL_EXSTYLE)인지 지정.
    /// 반환값은 변경 전 값.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

    /// <summary>창의 현재 스타일 값을 읽기. (비트 제거/추가를 위해 먼저 현재 값을 읽을 때 사용)</summary>
    [DllImport("user32.dll")]
    public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    /// <summary>
    /// 레이어드 창의 균일 알파/색상키 설정. ※ 본 데모는 호출하지 않음(DWM 투명과 충돌 우려).
    /// crKey: 색상키, bAlpha: 0~255 알파, dwFlags: LWA_ALPHA / LWA_COLORKEY.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    /// <summary>창의 화면상 사각 영역(RECT)을 가져온다. 커서가 창 안 어디인지 계산할 때 사용.</summary>
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    /// <summary>
    /// 창의 위치·크기·Z순서를 한 번에 설정. uFlags(SWP_*)로 무엇을 적용/생략할지 제어.
    /// hWndInsertAfter 에 HWND_TOPMOST 등을 주면 Z순서(항상 위)까지 조정.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    /// <summary>창에 윈도우 메시지를 보낸다. 창 드래그(WM_SYSCOMMAND) 트리거에 사용.</summary>
    [DllImport("user32.dll")]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

    /// <summary>
    /// 현재 마우스 캡처를 해제. 창 드래그 직전에 호출 →
    /// 이어지는 WM_SYSCOMMAND(SC_MOVE) 가 "타이틀바를 잡은 것"처럼 동작하게 만든다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool ReleaseCapture();

    /// <summary>
    /// 데스크톱 전역 커서 위치(POINT)를 가져온다.
    /// 클릭스루(WS_EX_TRANSPARENT) 상태에선 Unity가 마우스 메시지를 못 받으므로,
    /// 이 전역 폴링으로 커서 위치를 직접 얻어 UI 위인지 판정한다.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT lpPoint);

    public const uint SPI_GETWORKAREA = 0x0030; // SystemParametersInfo 액션: 작업 영역(작업표시줄 제외) 조회

    /// <summary>
    /// 시스템 파라미터를 조회/설정한다. 여기선 SPI_GETWORKAREA 로 주 모니터의
    /// "작업 영역"(작업표시줄을 제외한 사용 가능 화면 사각형)을 RECT 로 받는다.
    /// → 창을 9분할 위치로 배치할 때 작업표시줄에 가려지지 않게 계산하는 데 사용.
    /// </summary>
    [DllImport("user32.dll")]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref RECT pvParam, uint fWinIni);

    public const int SM_CXSCREEN = 0; // GetSystemMetrics: 주 모니터 전체 가로 픽셀(작업표시줄 포함)
    public const int SM_CYSCREEN = 1; // GetSystemMetrics: 주 모니터 전체 세로 픽셀(작업표시줄 포함)

    /// <summary>
    /// 시스템 지표를 조회한다. SM_CXSCREEN/SM_CYSCREEN 으로 주 모니터의 "전체" 해상도를 얻는다.
    /// → 창 크기를 "화면 세로의 1/3·1/2" 처럼 모니터 비례로 계산하는 데 사용.
    /// (SPI_GETWORKAREA 와 같은 좌표계 — DPI 가상화 환경에서도 서로 일관된다.)
    /// </summary>
    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    // ──────────────────────────────────────────────────────────────
    // dwmapi.dll — 데스크톱 합성기(투명/유리 효과)
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// DWM 프레임(유리 영역)을 클라이언트 영역으로 확장한다.
    /// MARGINS 의 각 변을 -1 로 주면 창 전체로 확장 → 카메라가 알파 0으로 그린 영역이
    /// 그대로 투명해져 바탕화면이 비친다. (Built-in 투명의 핵심 호출)
    /// 반환값은 HRESULT(0이면 성공).
    /// </summary>
    [DllImport("dwmapi.dll")]
    public static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);
}
