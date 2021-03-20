﻿/* 
 Windows Virtual Key Wrapper ver 1.0.0
 Written by Byulbram 2016 (byulbram@ck.ac.kr)
 Byulbram Studio / ChungKang College of Cultural Industry
 
 This Code was written for bypass Unity KeyCode input problem in non-english IME keyboard

 How To Use:
    Just insert "using WindowsInput" to your code 
    and use WinInput.GetKey() instead of Input.GetKey()
    This will work on Windows PC Platform 
*/
using System.Collections;
using UnityEngine;

#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR_OSX)
using System.Runtime.InteropServices;
#else
using KeyCode = UnityEngine.KeyCode;
using Input = UnityEngine.Input;
#endif

namespace WindowsInput
{

	public class WinInput
	{
#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR_OSX)
        [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true, CallingConvention = CallingConvention.Winapi)]
        protected static extern short GetAsyncKeyState(int keyCode);

        // Mapper for Unity KeyCode to Virtual KeyCode with full key set
        private static int KeyCodeToVkeyFullSet(KeyCode key) 
        {
            int VK = 0;
            switch (key)
            {
                case KeyCode.Backspace: VK = 0x08; break;
                case KeyCode.Tab: VK = 0x09; break;
                case KeyCode.Clear: VK = 0x0C; break;
                case KeyCode.Return: VK = 0x0D; break;
                case KeyCode.Pause: VK = 0x13; break;
                case KeyCode.Escape: VK = 0x1B; break;
                case KeyCode.Space: VK = 0x20; break;
                case KeyCode.Exclaim: VK = 0x31; break;
                case KeyCode.DoubleQuote: VK = 0xDE; break;
                case KeyCode.Hash: VK = 0x33; break;
                case KeyCode.Dollar: VK = 0x34; break;
                case KeyCode.Ampersand: VK = 0x37; break;
                case KeyCode.Quote: VK = 0xDE; break;
                case KeyCode.LeftParen: VK = 0x39; break;
                case KeyCode.RightParen: VK = 0x30; break;
                case KeyCode.Asterisk: VK = 0x13; break;
                case KeyCode.Equals: 
                case KeyCode.Plus: VK = 0xBB; break;
                case KeyCode.Less: 
                case KeyCode.Comma: VK = 0xBC; break;
                case KeyCode.Underscore: 
                case KeyCode.Minus: VK = 0xBD; break;
                case KeyCode.Greater: 
                case KeyCode.Period: VK = 0xBE; break;
                case KeyCode.Question: 
                case KeyCode.Slash: VK = 0xBF; break;

                case KeyCode.Alpha0:
                case KeyCode.Alpha1:
                case KeyCode.Alpha2:
                case KeyCode.Alpha3:
                case KeyCode.Alpha4:
                case KeyCode.Alpha5:
                case KeyCode.Alpha6:
                case KeyCode.Alpha7:
                case KeyCode.Alpha8:
                case KeyCode.Alpha9:
                    VK = 0x30 + ((int)key - (int)KeyCode.Alpha0); break;
                case KeyCode.Colon: 
                case KeyCode.Semicolon: VK = 0xBA; break;

                case KeyCode.At: VK = 0x32; break;
                case KeyCode.LeftBracket: VK = 0xDB; break;
                case KeyCode.Backslash: VK = 0xDC; break;
                case KeyCode.RightBracket: VK = 0xDD; break;
                case KeyCode.Caret: VK = 0x36; break;                
                case KeyCode.BackQuote: VK = 0xC0; break;

                case KeyCode.A:
                case KeyCode.B:
                case KeyCode.C:
                case KeyCode.D:
                case KeyCode.E:
                case KeyCode.F:
                case KeyCode.G:
                case KeyCode.H:
                case KeyCode.I:
                case KeyCode.J:
                case KeyCode.K:
                case KeyCode.L:
                case KeyCode.M:
                case KeyCode.N:
                case KeyCode.O:
                case KeyCode.P:
                case KeyCode.Q:
                case KeyCode.R:
                case KeyCode.S:
                case KeyCode.T:
                case KeyCode.U:
                case KeyCode.V:
                case KeyCode.W:
                case KeyCode.X:
                case KeyCode.Y:
                case KeyCode.Z:
                    VK = 0x41 + ((int)key - (int)KeyCode.A); break;

                case KeyCode.Delete: VK = 0x2E; break;
                case KeyCode.Keypad0:
                case KeyCode.Keypad1:
                case KeyCode.Keypad2:
                case KeyCode.Keypad3:
                case KeyCode.Keypad4:
                case KeyCode.Keypad5:
                case KeyCode.Keypad6:
                case KeyCode.Keypad7:
                case KeyCode.Keypad8:
                case KeyCode.Keypad9:
                    VK = 0x60 + ((int)key - (int)KeyCode.Keypad0); break;

                case KeyCode.KeypadPeriod: VK = 0x6E; break;
                case KeyCode.KeypadDivide: VK = 0x6F; break;
                case KeyCode.KeypadMultiply: VK = 0x6A; break;
                case KeyCode.KeypadMinus: VK = 0x6D; break;
                case KeyCode.KeypadPlus: VK = 0x6B; break;                
                case KeyCode.KeypadEnter: VK = 0x6C; break;
                //case KeyCode.KeypadEquals: VK = 0x00; break;
                case KeyCode.UpArrow: VK = 0x26; break;
                case KeyCode.DownArrow: VK = 0x28; break;
                case KeyCode.RightArrow: VK = 0x27; break;
                case KeyCode.LeftArrow: VK = 0x25; break;
                case KeyCode.Insert: VK = 0x2D; break;                
                case KeyCode.Home: VK = 0x24; break;
                case KeyCode.End: VK = 0x23; break;
                case KeyCode.PageUp: VK = 0x21; break;
                case KeyCode.PageDown: VK = 0x22; break;

                case KeyCode.F1:
                case KeyCode.F2:
                case KeyCode.F3:
                case KeyCode.F4:
                case KeyCode.F5:
                case KeyCode.F6:
                case KeyCode.F7:
                case KeyCode.F8:
                case KeyCode.F9:
                case KeyCode.F10:
                case KeyCode.F11:
                case KeyCode.F12:
                case KeyCode.F13:
                case KeyCode.F14:
                case KeyCode.F15:
                    VK = 0x70 + ((int)key - (int)KeyCode.F1); break;

                case KeyCode.Numlock: VK = 0x90; break;
                case KeyCode.CapsLock: VK = 0x14; break;
                case KeyCode.ScrollLock: VK = 0x91; break;
                case KeyCode.RightShift: VK = 0xA1; break;
                case KeyCode.LeftShift: VK = 0xA0; break;
                case KeyCode.RightControl: VK = 0xA3; break;
                case KeyCode.LeftControl: VK = 0xA2; break;
                case KeyCode.RightAlt: VK = 0xA5; break;
                case KeyCode.LeftAlt: VK = 0xA4; break;
                //case KeyCode.RightCommand: VK = 0x22; break;
                //case KeyCode.RightApple: VK = 0x22; break;
                //case KeyCode.LeftCommand: VK = 0x22; break;
                //case KeyCode.LeftApple: VK = 0x22; break;
                //case KeyCode.LeftWindows: VK = 0x22; break;
                //case KeyCode.RightWindows: VK = 0x22; break;
                case KeyCode.Help: VK = 0xE3; break;
                case KeyCode.Print: VK = 0x2A; break;
                case KeyCode.SysReq: VK = 0x2C; break;
                case KeyCode.Break: VK = 0x03; break;
            }
            return VK;
        }

        // Simplified Mapper for Unity KeyCode to Virtual KeyCode with Alphabet key set
        private static int KeyCodeToVkey(KeyCode key)
        {
            int VK = 0;
            switch (key)
            {
                case KeyCode.A:
                case KeyCode.B:
                case KeyCode.C:
                case KeyCode.D:
                case KeyCode.E:
                case KeyCode.F:
                case KeyCode.G:
                case KeyCode.H:
                case KeyCode.I:
                case KeyCode.J:
                case KeyCode.K:
                case KeyCode.L:
                case KeyCode.M:
                case KeyCode.N:
                case KeyCode.O:
                case KeyCode.P:
                case KeyCode.Q:
                case KeyCode.R:
                case KeyCode.S:
                case KeyCode.T:
                case KeyCode.U:
                case KeyCode.V:
                case KeyCode.W:
                case KeyCode.X:
                case KeyCode.Y:
                case KeyCode.Z:
                    VK = 0x41 + ((int)key - (int)KeyCode.A); break;
            }
            return VK;
        }
#endif

		// GetKey with GetAsyncKeyState for IME Alphabets
		public static bool GetKey(KeyCode key)
		{
#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR_OSX)
            int VK = KeyCodeToVkey(key);
            if (VK != 0)
                return (GetAsyncKeyState(VK) != 0); 
            //else
                //return Input.GetKey(key);
			return false;
#else
			return Input.GetKey(key);
#endif
		}

		// GetKeyDown with GetAsyncKeyState for IME Alphabets
		public static bool GetKeyDown(KeyCode key)
		{
#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR_OSX)
            return (GetAsyncKeyState(KeyCodeToVkey(key)) == -32767);
#else
			return Input.GetKeyDown(key);
#endif
		}

		// GetKey with GetAsyncKeyState for full keycode
		public static bool GetKeyFullCover(KeyCode key)
		{
#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR_OSX)
            int VK = KeyCodeToVkeyFullSet(key);
            if (VK != 0)
                return (GetAsyncKeyState(VK) != 0);
            //else
                //return Input.GetKey(key);
			return false;
#else
			return Input.GetKey(key);
#endif
		}

#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR_OSX)
// Key Check with Virtual Keycode
        public static bool GetKeyVK(int VKey)
        {
            return (GetAsyncKeyState(VKey) != 0);
        }
#endif


	}

	public enum KeyCode
	{
		A = 0x61,
		Alpha0 = 0x30,
		Alpha1 = 0x31,
		Alpha2 = 50,
		Alpha3 = 0x33,
		Alpha4 = 0x34,
		Alpha5 = 0x35,
		Alpha6 = 0x36,
		Alpha7 = 0x37,
		Alpha8 = 0x38,
		Alpha9 = 0x39,
		AltGr = 0x139,
		Ampersand = 0x26,
		Asterisk = 0x2a,
		At = 0x40,
		B = 0x62,
		BackQuote = 0x60,
		Backslash = 0x5c,
		Backspace = 8,
		Break = 0x13e,
		C = 0x63,
		CapsLock = 0x12d,
		Caret = 0x5e,
		Clear = 12,
		Colon = 0x3a,
		Comma = 0x2c,
		D = 100,
		Delete = 0x7f,
		Dollar = 0x24,
		DoubleQuote = 0x22,
		DownArrow = 0x112,
		E = 0x65,
		End = 0x117,
		Equals = 0x3d,
		Escape = 0x1b,
		Exclaim = 0x21,
		F = 0x66,
		F1 = 0x11a,
		F10 = 0x123,
		F11 = 0x124,
		F12 = 0x125,
		F13 = 0x126,
		F14 = 0x127,
		F15 = 0x128,
		F2 = 0x11b,
		F3 = 0x11c,
		F4 = 0x11d,
		F5 = 0x11e,
		F6 = 0x11f,
		F7 = 0x120,
		F8 = 0x121,
		F9 = 290,
		G = 0x67,
		Greater = 0x3e,
		H = 0x68,
		Hash = 0x23,
		Help = 0x13b,
		Home = 0x116,
		I = 0x69,
		Insert = 0x115,
		J = 0x6a,
		Joystick1Button0 = 350,
		Joystick1Button1 = 0x15f,
		Joystick1Button10 = 360,
		Joystick1Button11 = 0x169,
		Joystick1Button12 = 0x16a,
		Joystick1Button13 = 0x16b,
		Joystick1Button14 = 0x16c,
		Joystick1Button15 = 0x16d,
		Joystick1Button16 = 0x16e,
		Joystick1Button17 = 0x16f,
		Joystick1Button18 = 0x170,
		Joystick1Button19 = 0x171,
		Joystick1Button2 = 0x160,
		Joystick1Button3 = 0x161,
		Joystick1Button4 = 0x162,
		Joystick1Button5 = 0x163,
		Joystick1Button6 = 0x164,
		Joystick1Button7 = 0x165,
		Joystick1Button8 = 0x166,
		Joystick1Button9 = 0x167,
		Joystick2Button0 = 370,
		Joystick2Button1 = 0x173,
		Joystick2Button10 = 380,
		Joystick2Button11 = 0x17d,
		Joystick2Button12 = 0x17e,
		Joystick2Button13 = 0x17f,
		Joystick2Button14 = 0x180,
		Joystick2Button15 = 0x181,
		Joystick2Button16 = 0x182,
		Joystick2Button17 = 0x183,
		Joystick2Button18 = 0x184,
		Joystick2Button19 = 0x185,
		Joystick2Button2 = 0x174,
		Joystick2Button3 = 0x175,
		Joystick2Button4 = 0x176,
		Joystick2Button5 = 0x177,
		Joystick2Button6 = 0x178,
		Joystick2Button7 = 0x179,
		Joystick2Button8 = 0x17a,
		Joystick2Button9 = 0x17b,
		Joystick3Button0 = 390,
		Joystick3Button1 = 0x187,
		Joystick3Button10 = 400,
		Joystick3Button11 = 0x191,
		Joystick3Button12 = 0x192,
		Joystick3Button13 = 0x193,
		Joystick3Button14 = 0x194,
		Joystick3Button15 = 0x195,
		Joystick3Button16 = 0x196,
		Joystick3Button17 = 0x197,
		Joystick3Button18 = 0x198,
		Joystick3Button19 = 0x199,
		Joystick3Button2 = 0x188,
		Joystick3Button3 = 0x189,
		Joystick3Button4 = 0x18a,
		Joystick3Button5 = 0x18b,
		Joystick3Button6 = 0x18c,
		Joystick3Button7 = 0x18d,
		Joystick3Button8 = 0x18e,
		Joystick3Button9 = 0x18f,
		Joystick4Button0 = 410,
		Joystick4Button1 = 0x19b,
		Joystick4Button10 = 420,
		Joystick4Button11 = 0x1a5,
		Joystick4Button12 = 0x1a6,
		Joystick4Button13 = 0x1a7,
		Joystick4Button14 = 0x1a8,
		Joystick4Button15 = 0x1a9,
		Joystick4Button16 = 0x1aa,
		Joystick4Button17 = 0x1ab,
		Joystick4Button18 = 0x1ac,
		Joystick4Button19 = 0x1ad,
		Joystick4Button2 = 0x19c,
		Joystick4Button3 = 0x19d,
		Joystick4Button4 = 0x19e,
		Joystick4Button5 = 0x19f,
		Joystick4Button6 = 0x1a0,
		Joystick4Button7 = 0x1a1,
		Joystick4Button8 = 0x1a2,
		Joystick4Button9 = 0x1a3,
		Joystick5Button0 = 430,
		Joystick5Button1 = 0x1af,
		Joystick5Button10 = 440,
		Joystick5Button11 = 0x1b9,
		Joystick5Button12 = 0x1ba,
		Joystick5Button13 = 0x1bb,
		Joystick5Button14 = 0x1bc,
		Joystick5Button15 = 0x1bd,
		Joystick5Button16 = 0x1be,
		Joystick5Button17 = 0x1bf,
		Joystick5Button18 = 0x1c0,
		Joystick5Button19 = 0x1c1,
		Joystick5Button2 = 0x1b0,
		Joystick5Button3 = 0x1b1,
		Joystick5Button4 = 0x1b2,
		Joystick5Button5 = 0x1b3,
		Joystick5Button6 = 0x1b4,
		Joystick5Button7 = 0x1b5,
		Joystick5Button8 = 0x1b6,
		Joystick5Button9 = 0x1b7,
		Joystick6Button0 = 450,
		Joystick6Button1 = 0x1c3,
		Joystick6Button10 = 460,
		Joystick6Button11 = 0x1cd,
		Joystick6Button12 = 0x1ce,
		Joystick6Button13 = 0x1cf,
		Joystick6Button14 = 0x1d0,
		Joystick6Button15 = 0x1d1,
		Joystick6Button16 = 0x1d2,
		Joystick6Button17 = 0x1d3,
		Joystick6Button18 = 0x1d4,
		Joystick6Button19 = 0x1d5,
		Joystick6Button2 = 0x1c4,
		Joystick6Button3 = 0x1c5,
		Joystick6Button4 = 0x1c6,
		Joystick6Button5 = 0x1c7,
		Joystick6Button6 = 0x1c8,
		Joystick6Button7 = 0x1c9,
		Joystick6Button8 = 0x1ca,
		Joystick6Button9 = 0x1cb,
		Joystick7Button0 = 470,
		Joystick7Button1 = 0x1d7,
		Joystick7Button10 = 480,
		Joystick7Button11 = 0x1e1,
		Joystick7Button12 = 0x1e2,
		Joystick7Button13 = 0x1e3,
		Joystick7Button14 = 0x1e4,
		Joystick7Button15 = 0x1e5,
		Joystick7Button16 = 0x1e6,
		Joystick7Button17 = 0x1e7,
		Joystick7Button18 = 0x1e8,
		Joystick7Button19 = 0x1e9,
		Joystick7Button2 = 0x1d8,
		Joystick7Button3 = 0x1d9,
		Joystick7Button4 = 0x1da,
		Joystick7Button5 = 0x1db,
		Joystick7Button6 = 0x1dc,
		Joystick7Button7 = 0x1dd,
		Joystick7Button8 = 0x1de,
		Joystick7Button9 = 0x1df,
		Joystick8Button0 = 490,
		Joystick8Button1 = 0x1eb,
		Joystick8Button10 = 500,
		Joystick8Button11 = 0x1f5,
		Joystick8Button12 = 0x1f6,
		Joystick8Button13 = 0x1f7,
		Joystick8Button14 = 0x1f8,
		Joystick8Button15 = 0x1f9,
		Joystick8Button16 = 0x1fa,
		Joystick8Button17 = 0x1fb,
		Joystick8Button18 = 0x1fc,
		Joystick8Button19 = 0x1fd,
		Joystick8Button2 = 0x1ec,
		Joystick8Button3 = 0x1ed,
		Joystick8Button4 = 0x1ee,
		Joystick8Button5 = 0x1ef,
		Joystick8Button6 = 0x1f0,
		Joystick8Button7 = 0x1f1,
		Joystick8Button8 = 0x1f2,
		Joystick8Button9 = 0x1f3,
		JoystickButton0 = 330,
		JoystickButton1 = 0x14b,
		JoystickButton10 = 340,
		JoystickButton11 = 0x155,
		JoystickButton12 = 0x156,
		JoystickButton13 = 0x157,
		JoystickButton14 = 0x158,
		JoystickButton15 = 0x159,
		JoystickButton16 = 0x15a,
		JoystickButton17 = 0x15b,
		JoystickButton18 = 0x15c,
		JoystickButton19 = 0x15d,
		JoystickButton2 = 0x14c,
		JoystickButton3 = 0x14d,
		JoystickButton4 = 0x14e,
		JoystickButton5 = 0x14f,
		JoystickButton6 = 0x150,
		JoystickButton7 = 0x151,
		JoystickButton8 = 0x152,
		JoystickButton9 = 0x153,
		K = 0x6b,
		Keypad0 = 0x100,
		Keypad1 = 0x101,
		Keypad2 = 0x102,
		Keypad3 = 0x103,
		Keypad4 = 260,
		Keypad5 = 0x105,
		Keypad6 = 0x106,
		Keypad7 = 0x107,
		Keypad8 = 0x108,
		Keypad9 = 0x109,
		KeypadDivide = 0x10b,
		KeypadEnter = 0x10f,
		KeypadEquals = 0x110,
		KeypadMinus = 0x10d,
		KeypadMultiply = 0x10c,
		KeypadPeriod = 0x10a,
		KeypadPlus = 270,
		L = 0x6c,
		LeftAlt = 0x134,
		LeftApple = 310,
		LeftArrow = 0x114,
		LeftBracket = 0x5b,
		LeftCommand = 310,
		LeftControl = 0x132,
		LeftParen = 40,
		LeftShift = 0x130,
		LeftWindows = 0x137,
		Less = 60,
		M = 0x6d,
		Menu = 0x13f,
		Minus = 0x2d,
		Mouse0 = 0x143,
		Mouse1 = 0x144,
		Mouse2 = 0x145,
		Mouse3 = 0x146,
		Mouse4 = 0x147,
		Mouse5 = 0x148,
		Mouse6 = 0x149,
		N = 110,
		None = 0,
		Numlock = 300,
		O = 0x6f,
		P = 0x70,
		PageDown = 0x119,
		PageUp = 280,
		Pause = 0x13,
		Period = 0x2e,
		Plus = 0x2b,
		Print = 0x13c,
		Q = 0x71,
		Question = 0x3f,
		Quote = 0x27,
		R = 0x72,
		Return = 13,
		RightAlt = 0x133,
		RightApple = 0x135,
		RightArrow = 0x113,
		RightBracket = 0x5d,
		RightCommand = 0x135,
		RightControl = 0x131,
		RightParen = 0x29,
		RightShift = 0x12f,
		RightWindows = 0x138,
		S = 0x73,
		ScrollLock = 0x12e,
		Semicolon = 0x3b,
		Slash = 0x2f,
		Space = 0x20,
		SysReq = 0x13d,
		T = 0x74,
		Tab = 9,
		U = 0x75,
		Underscore = 0x5f,
		UpArrow = 0x111,
		V = 0x76,
		W = 0x77,
		X = 120,
		Y = 0x79,
		Z = 0x7a
	}
}
