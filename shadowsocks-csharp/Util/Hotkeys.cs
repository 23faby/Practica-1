﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;
using GlobalHotKey;
using Shadowsocks.Controller;

namespace Shadowsocks.Util
{
    public static class HotKeys
    {
        private static HotKeyManager _hotKeyManager;

        public delegate void HotKeyCallBackHandler();
        // map key and corresponding handler function
        private static Dictionary<HotKey, HotKeyCallBackHandler> keymap = new Dictionary<HotKey, HotKeyCallBackHandler>();

        public static void Init()
        {
            _hotKeyManager = new HotKeyManager();
            _hotKeyManager.KeyPressed += HotKeyManagerPressed;
        }

        public static void Destroy()
        {
            // He will unreg all keys and dispose resources
            _hotKeyManager.Dispose();
        }

        static void HotKeyManagerPressed(object sender, KeyPressedEventArgs e)
        {
            var hotkey = e.HotKey;
            HotKeyCallBackHandler callback;
            if (keymap.TryGetValue(hotkey, out callback))
                callback();
        }
        
        public static bool IsExist( HotKey hotKey ) { return keymap.Any( v => v.Key.Equals( hotKey ) ); }

        public static string HotKey2str( HotKey key ) { return HotKey2str( key.Key, key.Modifiers ); }

        public static string HotKey2str( Key key, ModifierKeys modifier ) {
            var keyNum = ( int ) key;
            var modifierNum = ( int ) modifier;
            return $"{keyNum}|{modifierNum}";
        }

        public static HotKey ParseHotKeyFromConfig( string s ) {
            if (s.IsNullOrEmpty()) return null;
            string[] strings = s.Split( '|' );
            var key = (Key)int.Parse(strings[ 0 ]);
            var modifierCombination = (ModifierKeys)int.Parse(strings[ 1 ]);
            if ( ! ModifierKeysConverter.IsDefinedModifierKeys( modifierCombination ) ) return null;
            return new HotKey(key, modifierCombination);
        }

        public static HotKey ParseHotKeyFromScreen( string s ) {
            try {
                if ( s.IsNullOrEmpty() ) return null;
                int offset = s.LastIndexOf( "+", StringComparison.OrdinalIgnoreCase );
                if (offset <= 0) return null;
                string modifierStr = s.Substring( 0, offset ).Trim();
                string keyStr = s.Substring( offset + 1 ).Trim();

                KeyConverter kc = new KeyConverter();
                ModifierKeysConverter mkc = new ModifierKeysConverter();
                Key key = ( Key ) kc.ConvertFrom( keyStr.ToUpper() );
                ModifierKeys modifier = ( ModifierKeys ) mkc.ConvertFrom( modifierStr );

                return new HotKey(key, modifier);
            } catch ( NotSupportedException ) {
                // converter exception
                return null;
            }
        }

        public static string DisplayHotKey( HotKey key ) { return DisplayHotKey( key.Key, key.Modifiers ); }

        public static string DisplayHotKey( Key key, ModifierKeys modifier ) {
            string str = "";
            if ( modifier.HasFlag( ModifierKeys.Control ) )
                str += "Ctrl + ";
            if ( modifier.HasFlag( ModifierKeys.Shift ) )
                str += "Shift + ";
            if ( modifier.HasFlag( ModifierKeys.Alt ) )
                str += "Alt + ";
            // In general, Win key is reserved by operating system
            // It leaves here just for sanity
            if ( modifier.HasFlag( ModifierKeys.Windows ) )
                str += "Win + ";
            str += key.ToString();
            return str;
        }

        public static int Regist( HotKey key, HotKeyCallBackHandler callBack ) {
            return Regist( key.Key, key.Modifiers, callBack );
        }

        public static int Regist(Key key, ModifierKeys modifiers, HotKeyCallBackHandler callBack)
        {
            try
            {
                _hotKeyManager.Register(key, modifiers);
                var hotkey = new HotKey(key, modifiers);
                if (IsExist(hotkey))
                {
                    // already registered
                    return -3;
                }
                keymap[hotkey] = callBack;
                return 0;
            }
            catch (ArgumentException)
            {
                // already registered
                // notify user to change key
                return -1;
            }
            catch (Win32Exception win32Exception)
            {
                // WinAPI error
                Logging.LogUsefulException(win32Exception);
                return -2;
            }
        }

        public static void UnRegist(HotKey key)
        {
            _hotKeyManager.Unregister(key);
        }

        public static void UnRegist(Key key, ModifierKeys modifiers)
        {
            _hotKeyManager.Unregister(key, modifiers);
        }

        public static IEnumerable<TControl> GetChildControls<TControl>(this Control control) where TControl : Control
        {
            var children = (control.Controls != null) ? control.Controls.OfType<TControl>() : Enumerable.Empty<TControl>();
            return children.SelectMany(c => GetChildControls<TControl>(c)).Concat(children);
        }

    }
}