using BarRaider.SdTools;
using InputInterceptorNS;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace KeyRemapper
{
    [PluginActionId("com.phischermen.keyremapper")]
    public class KeyRemapper : KeypadBase
    {
        private class PluginSettings
        {
            public static PluginSettings CreateDefaultSettings()
            {
                PluginSettings instance = new PluginSettings();
                instance.PedalTapTime = 1000;
                return instance;
            }
            
            [JsonProperty(PropertyName = "pedalTapTime")]
            public int PedalTapTime { get; set; }
        }
        
        private PluginSettings _settings;
        private KeyboardHook _keyboardHook;
        private MouseController _mouseController;
        private bool _anyKeyPressedDuringPedalDown;
        private bool _leftMouseControlDown;
        private bool _rightMouseControlDown;
        private DateTime _lastKeyPress = DateTime.MinValue;

        private readonly HashSet<KeyCode> ExceptKeys = new(new[] { KeyCode.Tab, KeyCode.Alt, KeyCode.RightShift, KeyCode.LeftShift });
        private readonly HashSet<KeyCode> ControlMouseKeys = new ( new[]{KeyCode.J, KeyCode.K, KeyCode.L, KeyCode.Semicolon, KeyCode.U, KeyCode.I});
        private readonly HashSet<KeyCode> MoveWithSelectKeys = new ( new[]{KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R});
        private readonly HashSet<KeyCode> RemappedKeys = new ( new[]{ KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.Space });

        #region PluginBase Methods

        public KeyRemapper(SDConnection connection, InitialPayload payload) : base(connection, payload)
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Constructor called");
            if (payload.Settings == null || payload.Settings.Count == 0)
            {
                _settings = PluginSettings.CreateDefaultSettings();
                Connection.SetSettingsAsync(JObject.FromObject(_settings));
            }
            else
            {
                _settings = payload.Settings.ToObject<PluginSettings>();
            }
            if (!InitializeDriver())
            {
                InstallDriver();
            }
        }

        public override void ReceivedSettings(ReceivedSettingsPayload payload)
        {
            Tools.AutoPopulateSettings(_settings, payload.Settings);
        }

        public override void ReceivedGlobalSettings(ReceivedGlobalSettingsPayload payload)
        {}

        public override void OnTick()
        {}


        public override void KeyPressed(KeyPayload payload)
        {
            _lastKeyPress = DateTime.Now;
            _anyKeyPressedDuringPedalDown = false;
            try
            {
                _keyboardHook = new KeyboardHook(KeyboardCallback);
                _mouseController = new MouseController();
            }
            catch (Exception e)
            {
                Logger.Instance.LogMessage(TracingLevel.ERROR, e.ToString());
            }
        }

        public override void KeyReleased(KeyPayload payload)
        {
            if (!_anyKeyPressedDuringPedalDown)
            {
                var delta = DateTime.Now - _lastKeyPress;
                if (delta.TotalMilliseconds < _settings.PedalTapTime)
                {
                    _keyboardHook?.SimulateKeyPress(KeyCode.Alt);
                }
            }
            ReleaseShift();
            _keyboardHook?.Dispose();
            _keyboardHook = null;
            _mouseController?.Dispose();
            _mouseController = null;
        }

        public override void Dispose()
        {
            Logger.Instance.LogMessage(TracingLevel.INFO, "Destructor called");
            _keyboardHook?.Dispose();
        }

        #endregion
        
        bool InitializeDriver() {
            if (InputInterceptor.CheckDriverInstalled()) {
                Logger.Instance.LogMessage( TracingLevel.INFO,"Input interceptor seems to be installed.");
                if (InputInterceptor.Initialize()) {
                    Logger.Instance.LogMessage( TracingLevel.INFO,"Input interceptor successfully initialized.");
                    return true;
                }
            }
            Logger.Instance.LogMessage( TracingLevel.ERROR,"Input interceptor initialization failed.");
            return false;
        }

        void InstallDriver() {
            Logger.Instance.LogMessage( TracingLevel.ERROR,"Input interceptor not installed.");
            if (InputInterceptor.CheckAdministratorRights()) {
                Logger.Instance.LogMessage( TracingLevel.INFO,"Installing...");
                if (InputInterceptor.InstallDriver()) {
                    Logger.Instance.LogMessage( TracingLevel.INFO,"Done! Restart your computer.");
                } else {
                    Logger.Instance.LogMessage( TracingLevel.ERROR,"Something... gone... wrong... :(");
                }
            } else {
                Logger.Instance.LogMessage( TracingLevel.INFO,"Restart program with administrator rights so it will be installed.");
            }
        }
        
        private void KeyboardCallback(ref KeyStroke stroke)
        {
            _anyKeyPressedDuringPedalDown = true;
            // Control mouse.
            if (ControlMouseKeys.Contains(stroke.Code))
            {
                switch (stroke.Code)
                {
                    case KeyCode.J:
                        _mouseController.leftPressed = stroke.State == KeyState.Down;
                        break;
                    case KeyCode.K:
                        _mouseController.upPressed = stroke.State == KeyState.Down;
                        break;
                    case KeyCode.L:
                        _mouseController.downPressed = stroke.State == KeyState.Down;
                        break;
                    case KeyCode.Semicolon:
                        _mouseController.rightPressed = stroke.State == KeyState.Down;
                        break;
                    case KeyCode.U:
                        if (_leftMouseControlDown == false && stroke.State == KeyState.Down)
                        {
                            _leftMouseControlDown = true;
                            _mouseController.SimulateLeftClick();
                        }
                        else if (_leftMouseControlDown && stroke.State == KeyState.Up)
                        {
                            _leftMouseControlDown = false;
                        }
                        break;
                    case KeyCode.I:
                        if (_rightMouseControlDown == false && stroke.State == KeyState.Down)
                        {
                            _rightMouseControlDown = true;
                            _mouseController.SimulateRightClick();
                        }
                        else if (_rightMouseControlDown && stroke.State == KeyState.Up)
                        {
                            _rightMouseControlDown = false;
                        }
                        break;
                }
                goto CancelKey;
            }
            // Cancel if key is not remapped.
            if (!ExceptKeys.Contains(stroke.Code) && !RemappedKeys.Contains(stroke.Code))
            {
                goto CancelKey;
            }
            UpdateShift(stroke); // for moving with selection.
            Remap(ref stroke);
            return;
            CancelKey:
            stroke.State = KeyState.Up;
        }
        
        private static void Remap(ref KeyStroke stroke)
        {
            stroke.Code = stroke.Code switch
            {
                KeyCode.A or KeyCode.Q => KeyCode.Left,
                KeyCode.S or KeyCode.W => KeyCode.Up,
                KeyCode.D or KeyCode.E => KeyCode.Down,
                KeyCode.F or KeyCode.R => KeyCode.Right,
                KeyCode.G => KeyCode.Home,
                KeyCode.H => KeyCode.End,
                KeyCode.T => KeyCode.PageUp,
                KeyCode.Y => KeyCode.PageDown,
                KeyCode.Space => KeyCode.Control,
                _ => stroke.Code,
            };
        }

        private void UpdateShift(KeyStroke stroke)
        {
            if (MoveWithSelectKeys.Contains(stroke.Code)) _keyboardHook?.SimulateKeyDown(KeyCode.LeftShift);
            else if (ExceptKeys.Contains(stroke.Code)) ReleaseShift();
        }

        private void ReleaseShift()
        {
            _keyboardHook?.SimulateKeyUp(KeyCode.LeftShift);
        }
    }
}
