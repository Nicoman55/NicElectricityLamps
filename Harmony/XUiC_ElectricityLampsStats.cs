using System.Globalization;
using UnityEngine;

namespace ElectricityLamps
{
    // Handles the UI controls for editing electricity lamp settings window.
    // Retrieves data from the associated TileEntityElectricityLightBlock and
    // updates UI elements such as labels, stats and controls to reflect the
    // current lamp settings and state.
    public class XUiC_ElectricityLampsStats : XUiController
    {
        // Holds a snapshot of light settings copied from one lamp so they
        // can be pasted onto another lamp without closing the settings window.
        private class LightSettingsClipboard
        {
            public int      LightMode;
            public bool     IsSpotLight;
            public float    LightIntensity;
            public float    LightRange;
            public ushort   LightKelvin;
            public Color    LightColor;
            public float    LightAngle;
            public LightStateType LightState;
            public float    Rate;
            public float    Delay;
        }

        // Shared across all instances so the clipboard survives window open/close.
        private static LightSettingsClipboard clipboard = null;
        public XUiC_ElectricityLampsWindowGroup Owner { get; set; }

        public TileEntityElectricityLightBlock TileEntity
        {
            get => tileEntity;
            set
            {
                tileEntity = value;

                if (SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer && tileEntity != null)
                {
                    powerItem = tileEntity.GetPowerItem();
                }
            }
        }

        // Finds all child UI controls and registers their value change handlers.
        public override void Init()
        {
            base.Init();

            CbxLightState = RegisterComboBox<LightStateType, XUiC_ComboBoxEnum<LightStateType>>(
                "CbxLightState",
                CbxLightState_OnValueChanged,
                "cbxLightState");
            cbxRate = RegisterComboBox<double, XUiC_ComboBoxFloat>(
                "cbxRate",
                CbxRate_OnValueChanged,
                "cbxRate");
            cbxDelay = RegisterComboBox<double, XUiC_ComboBoxFloat>(
                "cbxDelay",
                CbxDelay_OnValueChanged,
                "cbxDelay");
            uiUseKelvin = RegisterComboBox<bool, XUiC_ComboBoxBool>(
                "uiUseKelvin",
                UiUseKelvin_OnValueChanged,
                "uiUseKelvin");
            uiTemperature = RegisterComboBox<long, XUiC_ComboBoxInt>(
                "uiTemperature",
                UiTemperature_OnValueChanged,
                "uiTemperature");
            uiIntensity = RegisterComboBox<double, XUiC_ComboBoxFloat>(
                "uiIntensity",
                UiIntensity_OnValueChanged,
                "uiIntensity");
            uiBeamAngle = RegisterComboBox<double, XUiC_ComboBoxFloat>(
                "uiBeamAngle",
                UiBeamAngle_OnValueChanged,
                "uiBeamAngle");
            uiRange = RegisterComboBox<double, XUiC_ComboBoxFloat>(
                "uiRange",
                UiRange_OnValueChanged,
                "uiRange");
            uiColorPicker = (XUiC_ColorPicker)GetChildById("uiColorPicker");
            if (uiColorPicker != null)
            {
                uiColorPicker.OnSelectedColorChanged += UiColorPicker_OnValueChanged;
            }
            else
            {
                Debug.LogWarning("ElectricityLampsStats missing uiColorPicker");
            }

            // Locate copy / paste buttons and subscribe to their OnPress events directly
            btnCopySettings = GetChildById("btnCopySettings");
            if (btnCopySettings != null)
                btnCopySettings.OnPress += (sender, btn) => btnCopySettings_OnPressed(sender, btn);
            else
                Debug.LogWarning("ElectricityLampsStats missing btnCopySettings");

            btnPasteSettings = GetChildById("btnPasteSettings");
            if (btnPasteSettings != null)
                btnPasteSettings.OnPress += (sender, btn) => btnPasteSettings_OnPressed(sender, btn);
            else
                Debug.LogWarning("ElectricityLampsStats missing btnPasteSettings");

            btnClearClipboard = GetChildById("btnClearClipboard");
            if (btnClearClipboard != null)
                btnClearClipboard.OnPress += (sender, btn) => btnClearClipboard_OnPressed(sender, btn);
            else
                Debug.LogWarning("ElectricityLampsStats missing btnClearClipboard");

            // Hide paste and clear buttons by default; they appear after the first copy
            if (btnPasteSettings?.ViewComponent != null)
                btnPasteSettings.ViewComponent.IsVisible = false;
            if (btnClearClipboard?.ViewComponent != null)
                btnClearClipboard.ViewComponent.IsVisible = false;
        }

        // Updates the lamp delay when the delay combo box changes.
        private void CbxDelay_OnValueChanged(XUiController _sender, double _oldValue, double _newValue)
        {
            UpdateTileEntityLight(tileEntity => tileEntity.Delay = (float)_newValue);
        }

        // Updates the lamp mode when the light state combo box changes.
        private void CbxLightState_OnValueChanged(XUiController _sender, LightStateType _oldValue, LightStateType _newValue)
        {
            UpdateTileEntityLight(tileEntity => tileEntity.LightState = _newValue);
            ApplyVisibilityToUi();
        }

        // Updates the lamp blinking rate when the rate combo box changes.
        private void CbxRate_OnValueChanged(XUiController _sender, double _oldValue, double _newValue)
        {
            UpdateTileEntityLight(tileEntity => tileEntity.Rate = (float)_newValue);
        }

        // Updates the lamp color when the color picker changes.
        private void UiColorPicker_OnValueChanged(Color _newValue)
        {
            UpdateTileEntityLight(tileEntity => tileEntity.LightColor = _newValue);
        }

        // Updates the spotlight beam angle when the beam angle combo box changes.
        private void UiBeamAngle_OnValueChanged(XUiController _sender, double _oldValue, double _newValue)
        {
            UpdateTileEntityLight(tileEntity => tileEntity.LightAngle = (float)_newValue);
        }

        // Updates the lamp range when the range combo box changes.
        private void UiRange_OnValueChanged(XUiController _sender, double _oldValue, double _newValue)
        {
            UpdateTileEntityLight(tileEntity =>
            {
                tileEntity.LightRange = (float)_newValue;
                tileEntity.UpdateDynamicRequiredPower();    // range changes affect power requirements, so update power requirements as well
                tileEntity.UpdateLightState();              // updates visuals + logic
            });
            ApplyPowerWarningColors();
            //GameManager.Instance.StartCoroutine(ApplyPowerWarningColorsDelayed());
        }

        // Updates the lamp intensity when the intensity combo box changes.
        private void UiIntensity_OnValueChanged(XUiController _sender, double _oldValue, double _newValue)
        {
            UpdateTileEntityLight(tileEntity =>
            {
                tileEntity.LightIntensity = (float)_newValue;
                tileEntity.UpdateDynamicRequiredPower();    // intensity changes affect power requirements, so update power requirements as well
                tileEntity.UpdateLightState();              // updates visuals + logic
            });
            ApplyPowerWarningColors();
            //GameManager.Instance.StartCoroutine(ApplyPowerWarningColorsDelayed());
        }

        // Updates the Kelvin temperature when the temperature combo box changes.
        private void UiTemperature_OnValueChanged(XUiController _sender, long _oldValue, long _newValue)
        {
            UpdateTileEntityLight(tileEntity => tileEntity.LightKelvin = (ushort)_newValue);
        }

        // Switches between Kelvin and RGB color mode.
        private void UiUseKelvin_OnValueChanged(XUiController _sender, bool _oldValue, bool _newValue)
        {
            UpdateTileEntityLight(tileEntity => tileEntity.IsKelvinScale = _newValue);
            ApplyVisibilityToUi();
        }

        // Copies all current light settings into the static clipboard and shows the paste button.
        internal void btnCopySettings_OnPressed(XUiController _sender, int _mouseButton)
        {
            if (tileEntity == null)
                return;

            Audio.Manager.PlayButtonClick();

            clipboard = new LightSettingsClipboard
            {
                LightMode      = (int)tileEntity.LightMode,
                IsSpotLight    = tileEntity.IsSpotLight,
                LightIntensity = tileEntity.LightIntensity,
                LightRange     = tileEntity.LightRange,
                LightKelvin    = tileEntity.LightKelvin,
                LightColor     = tileEntity.LightColor,
                LightAngle     = tileEntity.LightAngle,
                LightState     = tileEntity.LightState,
                Rate           = tileEntity.Rate,
                Delay          = tileEntity.Delay,
            };

            if (btnPasteSettings?.ViewComponent != null)
                btnPasteSettings.ViewComponent.IsVisible = true;
            if (btnClearClipboard?.ViewComponent != null)
                btnClearClipboard.ViewComponent.IsVisible = true;
        }

        // Applies all clipboard settings to the current light and refreshes the full UI.
        internal void btnPasteSettings_OnPressed(XUiController _sender, int _mouseButton)
        {
            if (tileEntity == null || clipboard == null)
                return;

            Audio.Manager.PlayButtonClick();

            // Snapshot the old RequiredPower before paste so the headroom check can
            // correctly subtract this lamp's old share from the stale ConsumerDemand.
            PowerConsumer pc = tileEntity.PowerItem as PowerConsumerToggle
                            ?? tileEntity.PowerItem as PowerConsumer;
            int oldRequiredPower = pc != null ? (int)pc.RequiredPower : 0;

            UpdateTileEntityLight(te =>
            {
                // Only copy the Kelvin/color mode bit from the clipboard;
                // preserve the spot/point light type of the target.
                te.IsKelvinScale  = (clipboard.LightMode & 1) == 1;

                te.LightIntensity = clipboard.LightIntensity;
                te.LightRange     = clipboard.LightRange;
                te.LightKelvin    = clipboard.LightKelvin;
                te.LightColor     = clipboard.LightColor;
                te.LightState     = clipboard.LightState;
                te.Rate           = clipboard.Rate;
                te.Delay          = clipboard.Delay;

                // Only copy beam angle if both source and target are spotlights;
                // a point light's internal angle value is meaningless on a spotlight.
                if (clipboard.IsSpotLight && te.IsSpotLight)
                    te.LightAngle = clipboard.LightAngle;

                te.UpdateDynamicRequiredPower();
            });

            // Refresh all UI controls to show the newly pasted values
            ApplyTileEntityValuesToUi();
            ApplyVisibilityToUi();
            ApplyPowerWarningColors(oldRequiredPower);
            RefreshBindings(false);
        }

        // Clears the clipboard and hides the paste and clear buttons.
        internal void btnClearClipboard_OnPressed(XUiController _sender, int _mouseButton)
        {
            clipboard = null;
            Audio.Manager.PlayButtonClick();

            if (btnPasteSettings?.ViewComponent != null)
                btnPasteSettings.ViewComponent.IsVisible = false;
            if (btnClearClipboard?.ViewComponent != null)
                btnClearClipboard.ViewComponent.IsVisible = false;
        }

        // Reads a block property and returns a fallback value when the property is missing.
        public string GetBlockProperty(string name, string fallback)
        {
            var properties = Block.list[BlockType].Properties;
            if (properties == null || !properties.Values.ContainsKey(name))
            {
                return fallback;
            }
            return properties.Values[name];
        }

        // Applies min, max, and step values from blocks.xml directly to the float controls.
        private void ApplyBlockPropertyLimitsToUi()
        {
            if (this.uiIntensity != null)
            {
                this.uiIntensity.Min = GetBlockPropertyDouble("LightMinIntensity", 0.2);
                this.uiIntensity.Max = GetBlockPropertyDouble("LightMaxIntensity", 8.0);
                this.uiIntensity.IncrementSize = GetBlockPropertyDouble("LightIntensityStep", 0.1);
            }
            if (this.uiRange != null)
            {
                this.uiRange.Min = GetBlockPropertyDouble("LightMinRange", 0.0);
                this.uiRange.Max = GetBlockPropertyDouble("LightMaxRange", 50.0);
                this.uiRange.IncrementSize = GetBlockPropertyDouble("LightRangeStep", 1.0);
            }
            if (this.uiBeamAngle != null)
            {
                this.uiBeamAngle.Min = GetBlockPropertyDouble("LightMinAngle", 30.0);
                this.uiBeamAngle.Max = GetBlockPropertyDouble("LightMaxAngle", 180.0);
                this.uiBeamAngle.IncrementSize = GetBlockPropertyDouble("LightAngleStep", 3.0);
            }
        }

        // Reads a double value from the current block's properties.
        private double GetBlockPropertyDouble(string propertyName, double fallback)
        {
            return StringParsers.ParseDouble(
                GetBlockProperty(propertyName, fallback.ToCultureInvariantString()),
                0,
                -1,
                NumberStyles.Any
            );
        }

        // Refreshes UI bindings while the lamp settings window is active.
        public override void Update(float _dt)
        {
            if (GameManager.Instance == null || GameManager.Instance.World == null || tileEntity == null)
            {
                return;
            }
            base.Update(_dt);
            RefreshBindings(false);
        }

        // Shows or hides certain UI controls based on the current lamp mode and settings.
        private void ApplyVisibilityToUi()
        {
            bool isKelvinScale = this.TileEntity != null && this.TileEntity.IsKelvinScale;
            bool isColorScale = this.TileEntity != null && this.TileEntity.IsColorScale;
            bool isSpotLight = this.TileEntity != null && this.TileEntity.IsSpotLight;
            bool isNotStatic = this.TileEntity != null && this.TileEntity.LightState != LightStateType.Static;

            XUiController colorPicker = GetChildById("uiColorPicker");
            if (colorPicker != null && colorPicker.ViewComponent != null)
                colorPicker.ViewComponent.IsVisible = isColorScale;

            XUiController temperaturePanel = GetChildById("panelTemperature");
            if (temperaturePanel != null && temperaturePanel.ViewComponent != null)
                temperaturePanel.ViewComponent.IsVisible = isKelvinScale;

            XUiController beamAnglePanel = GetChildById("panelBeamAngle");
            if (beamAnglePanel != null && beamAnglePanel.ViewComponent != null)
                beamAnglePanel.ViewComponent.IsVisible = isSpotLight;

            // Hide rate and delay panels when light state is static
            XUiController ratePanel = GetChildById("panelRate");
            if (ratePanel != null && ratePanel.ViewComponent != null)
                ratePanel.ViewComponent.IsVisible = isNotStatic;

            XUiController delayPanel = GetChildById("panelDelay");
            if (delayPanel != null && delayPanel.ViewComponent != null)
                delayPanel.ViewComponent.IsVisible = isNotStatic;
        }

        // Loads the current lamp values into the UI when the window opens.
        public override void OnOpen()
        {
            base.OnOpen();
            if (this.TileEntity == null)
            {
                return;
            }
            BlockType = this.TileEntity.GetChunk().GetBlock(this.TileEntity.localChunkPos).type;
            ApplyBlockPropertyLimitsToUi();

            ApplyPowerWarningColors();

            ApplyTileEntityValuesToUi();
            ApplyVisibilityToUi();
            this.TileEntity.SetUserAccessing(true);
            this.TileEntity.SetModified();
            RefreshBindings(false);

            // Show paste and clear buttons only when clipboard has copied settings
            if (btnPasteSettings?.ViewComponent != null)
                btnPasteSettings.ViewComponent.IsVisible = (clipboard != null);
            if (btnClearClipboard?.ViewComponent != null)
                btnClearClipboard.ViewComponent.IsVisible = (clipboard != null);
        }

        // Saves UI changes and unlocks the tile entity when the window closes.
        public override void OnClose()
        {
            GameManager instance = GameManager.Instance;
            Vector3i worldPosition = tileEntity.ToWorldPos();

            if (!XUiC_CameraWindow.hackyIsOpeningMaximizedWindow)
            {
                tileEntity.SetUserAccessing(false);

                if (GetChildById("uiColorPicker") is XUiC_ColorPicker colorPicker)
                {
                    tileEntity.LightColor = colorPicker.SelectedColor;
                }
                instance.TEUnlockServer(tileEntity.GetClrIdx(), worldPosition, tileEntity.entityId, true);
                tileEntity.SetModified();
                powerItem = null;
            }

            base.OnClose();
        }

        // Registers a combo box and logs a warning if the control is missing.
        private TComboBox RegisterComboBox<TValue, TComboBox>(
            string childId,
            XUiC_ComboBox<TValue>.XUiEvent_ValueChanged valueChangedHandler,
            string warningName)
            where TComboBox : XUiC_ComboBox<TValue>
        {
            TComboBox comboBox = (TComboBox)GetChildById(childId);
            if (comboBox != null)
            {
                comboBox.OnValueChanged += valueChangedHandler;
            }
            else
            {
                Debug.LogWarning($"ElectricityLampsStats missing {warningName}");
            }
            return comboBox;
        }

        // Applies a change to the tile entity, updates the visible light, and refreshes UI bindings.
        private void UpdateTileEntityLight(System.Action<TileEntityElectricityLightBlock> applyChange)
        {
            if (TileEntity != null)
            {
                applyChange(TileEntity);

                BlockEntityData blockEntity = TileEntity.GetChunk().GetBlockEntity(TileEntity.ToWorldPos());
                if (blockEntity != null)
                {
                    TileEntity.UpdateLightState(blockEntity);
                }
            }
            RefreshBindings(false);
        }

        // Copies the tile entity state into the matching UI controls.
        private void ApplyTileEntityValuesToUi()
        {
            if (CbxLightState != null)
            {
                CbxLightState.Value = tileEntity.LightState;
            }
            if (cbxRate != null)
            {
                cbxRate.Value = tileEntity.Rate;
            }
            if (cbxDelay != null)
            {
                cbxDelay.Value = tileEntity.Delay;
            }
            if (uiColorPicker != null)
            {
                uiColorPicker.SelectedColor = tileEntity.LightColor;
            }
            if (uiUseKelvin != null)
            {
                uiUseKelvin.Value = TileEntity.IsKelvinScale;
            }
            if (uiIntensity != null)
            {
                uiIntensity.Value = tileEntity.LightIntensity;
            }
            if (uiTemperature != null)
            {
                uiTemperature.Value = tileEntity.LightKelvin;
            }
            if (uiRange != null)
            {
                uiRange.Value = tileEntity.LightRange;
            }
            if (uiBeamAngle != null)
            {
                uiBeamAngle.Value = tileEntity.LightAngle;
            }
        }

        // Converts a bool to the lowercase string expected by XUi bindings.
        private static string BoolToBindingValue(bool value)
        {
            return value ? "true" : "false";
        }

        // Returns the remaining power headroom available on the network in watts.
        // oldRequiredPower: if >= 0, overrides the value subtracted from ConsumerDemand
        // to account for the fact that ConsumerDemand may not yet reflect a recent paste.
        private int GetAvailablePowerHeadroom(int oldRequiredPower = -1)
        {
            if (this.TileEntity == null)
            {
                //Debug.Log("[ElectricityLamps] Headroom: TileEntity is null");
                return int.MaxValue;
            }

            PowerConsumer powerConsumer = this.TileEntity.PowerItem as PowerConsumerToggle
                                       ?? this.TileEntity.PowerItem as PowerConsumer;
            if (powerConsumer == null)
            {
                //Debug.Log("[ElectricityLamps] Headroom: PowerConsumer is null");
                return int.MaxValue;
            }

            if (powerConsumer.Parent == null)
            {
                //Debug.Log("[ElectricityLamps] Headroom: Parent is null");
                return int.MaxValue;
            }

            // Walk up the parent chain until we find a PowerSource
            PowerItem current = powerConsumer.Parent;
            PowerSource source = null;
            while (current != null)
            {
                if (current is PowerSource ps)
                {
                    source = ps;
                    break;
                }
                current = current.Parent;
            }

            if (source == null)
            {
                //Debug.Log("[ElectricityLamps] Headroom: No PowerSource found in parent chain");
                return int.MaxValue;
            }

            // Walk up to root
            while (source.Parent is PowerSource parentSource)
                source = parentSource;

            // If the root power source is turned off, no overcapacity warning makes sense:
            // consumers are not being powered anyway, so the warning would be a false positive.
            // This is specifically needed when running alongside OcbElectricityOverhaul, which
            // sets MaxGridProduction=0 when the source is off while ConsumerDemand still reflects
            // all registered consumers, causing a spurious negative headroom calculation.
            if (!source.IsOn)
                return int.MaxValue;

            //Debug.Log($"[ElectricityLamps] Root type: {source.GetType().Name}");

            // Try OCB path via reflection
            var sourceType = source.GetType();
            var maxGridField = sourceType.GetField("MaxGridProduction");
            var gridDemandField = sourceType.GetField("GridConsumerDemand");
            var consumerDemandField = sourceType.GetField("ConsumerDemand");

            //Debug.Log($"[ElectricityLamps] OCB fields found: MaxGridProduction={maxGridField != null}, GridConsumerDemand={gridDemandField != null}, ConsumerDemand={consumerDemandField != null}");

            if (maxGridField != null && gridDemandField != null && consumerDemandField != null)
            {
                int maxGrid = (int)(ushort)maxGridField.GetValue(source);
                int gridDemand = (int)(ushort)gridDemandField.GetValue(source);
                int consumerDemand = (int)(ushort)consumerDemandField.GetValue(source);
                // ConsumerDemand is a network-wide value updated on the OCB tick, so it
                // may be stale immediately after a paste. When oldRequiredPower is supplied,
                // use it to subtract this lamp's pre-paste share from ConsumerDemand so
                // we correctly isolate other consumers' load. Otherwise fall back to the
                // current RequiredPower (which is accurate for slider changes).
                int thisLampOldDemand = oldRequiredPower >= 0 ? oldRequiredPower : (int)powerConsumer.RequiredPower;
                int otherDemand = consumerDemand - thisLampOldDemand;
                int headroom = maxGrid - gridDemand - otherDemand - (int)this.TileEntity.PowerUsed;
                return headroom;
            }

            // Vanilla path
            int otherConsumers = (int)source.LastPowerUsed - (int)powerConsumer.RequiredPower;
            int vanillaHeadroom = (int)source.MaxOutput - otherConsumers - (int)this.TileEntity.PowerUsed;
            return vanillaHeadroom;
        }

        // Colors the intensity and range labels red when power consumption exceeds
        // the available network headroom, restoring white when within limits.
        // oldRequiredPower: if >= 0, used to correct stale ConsumerDemand after a paste.
        private void ApplyPowerWarningColors(int oldRequiredPower = -1)
        {
            int headroom = GetAvailablePowerHeadroom(oldRequiredPower);

            // Warn when there is no headroom left (network is at or over capacity)
            bool isExceeding = headroom != int.MaxValue && headroom <= 0;
            Color warningColor = isExceeding ? Color.red : Color.white;

            XUiV_Label lblIntensity = GetChildById("lblIntensity")?.ViewComponent as XUiV_Label;
            if (lblIntensity != null)
                lblIntensity.Color = warningColor;

            XUiV_Label lblRange = GetChildById("lblRange")?.ViewComponent as XUiV_Label;
            if (lblRange != null)
                lblRange.Color = warningColor;

            // Show or hide the power warning label
            XUiController lblPowerWarning = GetChildById("lblPowerWarning");
            if (lblPowerWarning?.ViewComponent != null)
                lblPowerWarning.ViewComponent.IsVisible = isExceeding;
        }

        private int BlockType;
        private TileEntityElectricityLightBlock tileEntity;
        private PowerItem powerItem;
        private XUiC_ComboBoxEnum<LightStateType> CbxLightState;
        private XUiC_ComboBoxFloat cbxRate;
        private XUiC_ComboBoxFloat cbxDelay;
        private XUiC_ColorPicker uiColorPicker;
        private XUiC_ComboBoxBool uiUseKelvin;
        private XUiC_ComboBoxInt uiTemperature;
        private XUiC_ComboBoxFloat uiIntensity;
        private XUiC_ComboBoxFloat uiBeamAngle;
        private XUiC_ComboBoxFloat uiRange;
        private XUiController btnCopySettings;
        private XUiController btnPasteSettings;
        private XUiController btnClearClipboard;
    }
}
