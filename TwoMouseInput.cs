using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

public class TwoMouseInput
{
    private const int RIDEV_INPUTSINK = 0x00000100;
    private const uint WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEMOUSE = 0;

    private const int GWLP_WNDPROC = -4;

    private IntPtr _oldWndProc;

    private readonly Vector2[] _deltas =
    {
        Vector2.Zero,
        Vector2.Zero
    };

    private readonly bool[] _leftClicks =
    {
        false,
        false
    };

    private readonly List<IntPtr> _mouseDevices = new();

    private WndProcDelegate? _wndProcDelegate;

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public IntPtr hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public IntPtr hDevice;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RAWMOUSEBUTTONS
    {
        [FieldOffset(0)]
        public uint ulButtons;

        [FieldOffset(0)]
        public ushort usButtonFlags;

        [FieldOffset(2)]
        public ushort usButtonData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort usFlags;
        public ushort padding;
        public RAWMOUSEBUTTONS buttons;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER header;
        public RAWMOUSE mouse;
    }

    private delegate IntPtr WndProcDelegate(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        RAWINPUTDEVICE[] pRawInputDevices,
        uint uiNumDevices,
        uint cbSize
    );

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr hRawInput,
        uint uiCommand,
        IntPtr pData,
        ref uint pcbSize,
        uint cbSizeHeader
    );

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr hWnd,
        int nIndex,
        IntPtr newLong
    );

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern IntPtr SetWindowLong32(
        IntPtr hWnd,
        int nIndex,
        IntPtr newLong
    );

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(
        IntPtr lpPrevWndFunc,
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam
    );

    // THIS is the method Program.cs is looking for
    public void Initialize(IntPtr windowHandle)
    {
        RAWINPUTDEVICE[] devices =
        {
            new RAWINPUTDEVICE
            {
                usUsagePage = 0x01,
                usUsage = 0x02,
                dwFlags = RIDEV_INPUTSINK,
                hwndTarget = windowHandle
            }
        };

        if (!RegisterRawInputDevices(
            devices,
            1,
            (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
        {
            throw new Exception("Could not register raw mouse input.");
        }

        _wndProcDelegate = WindowProcedure;

        IntPtr newWndProc =
            Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);

        if (IntPtr.Size == 8)
        {
            _oldWndProc = SetWindowLongPtr64(
                windowHandle,
                GWLP_WNDPROC,
                newWndProc
            );
        }
        else
        {
            _oldWndProc = SetWindowLong32(
                windowHandle,
                GWLP_WNDPROC,
                newWndProc
            );
        }
    }

    private IntPtr WindowProcedure(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (msg == WM_INPUT)
        {
            ProcessRawInput(lParam);
        }

        return CallWindowProc(
            _oldWndProc,
            hWnd,
            msg,
            wParam,
            lParam
        );
    }

    private void ProcessRawInput(IntPtr hRawInput)
    {
        uint size = 0;

        GetRawInputData(
            hRawInput,
            RID_INPUT,
            IntPtr.Zero,
            ref size,
            (uint)Marshal.SizeOf<RAWINPUTHEADER>()
        );

        if (size == 0)
            return;

        IntPtr buffer = Marshal.AllocHGlobal((int)size);

        try
        {
            uint result = GetRawInputData(
                hRawInput,
                RID_INPUT,
                buffer,
                ref size,
                (uint)Marshal.SizeOf<RAWINPUTHEADER>()
            );

            if (result == uint.MaxValue)
                return;

            RAWINPUT input =
                Marshal.PtrToStructure<RAWINPUT>(buffer);

            if (input.header.dwType != RIM_TYPEMOUSE)
                return;

            IntPtr device = input.header.hDevice;

            int mouseIndex = _mouseDevices.IndexOf(device);

            if (mouseIndex == -1)
            {
                if (_mouseDevices.Count >= 2)
                    return;

                _mouseDevices.Add(device);
                mouseIndex = _mouseDevices.Count - 1;
            }

            _deltas[mouseIndex] += new Vector2(
                input.mouse.lLastX,
                input.mouse.lLastY
            );

            if ((input.mouse.buttons.usButtonFlags & 0x0001) != 0)
            {
                _leftClicks[mouseIndex] = true;
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public Vector2 GetDelta(int mouseIndex)
    {
        if (mouseIndex < 0 || mouseIndex > 1)
            return Vector2.Zero;

        Vector2 delta = _deltas[mouseIndex];

        _deltas[mouseIndex] = Vector2.Zero;

        return delta;
    }

    public bool GetLeftClick(int mouseIndex)
    {
        if (mouseIndex < 0 || mouseIndex > 1)
            return false;

        bool clicked = _leftClicks[mouseIndex];
        _leftClicks[mouseIndex] = false;
        return clicked;
    }
}