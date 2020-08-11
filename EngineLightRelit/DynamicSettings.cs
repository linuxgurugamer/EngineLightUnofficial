using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using KSP.UI.Screens;
using UnityEngine;
using ToolbarControl_NS;
using ClickThroughFix;

namespace EngineLightRelit
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        protected void Start()
        {
            ToolbarControl.RegisterMod(DynamicSettings.MODID, DynamicSettings.MODNAME);
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]

    public class DynamicSettings : MonoBehaviour
    {
        internal const string MODID = "EngineLight";
        internal const string MODNAME = "Engine Light Relit";

        static ToolbarControl toolbarControl;

        private const string _stockIcon = "EngineLightRelit/PluginData/button-38";
        private const string _blizzyIcon = "EngineLightRelit/PluginData/button-24";

        Rect win;
        bool visible = false;

        internal static bool ConfigChanged = false;

        void Start()
        {
            win = new Rect(100, 100, 400, 175);
            if (toolbarControl == null) CreateToolbar();
        }
        private void CreateToolbar()
        {
            if (toolbarControl == null)
            {
                toolbarControl = this.gameObject.AddComponent<ToolbarControl>();
                toolbarControl.AddToAllToolbars(
                    Toggle, Toggle,
                    ApplicationLauncher.AppScenes.FLIGHT,
                    MODID,
                    "EnglineLightBtn",
                   _stockIcon,
                   _blizzyIcon,
                    MODNAME
                    );
            }
        }


        void Toggle()
        {
            visible = !visible;
            ConfigChanged = false;
        }
        int id = 457984233;
        void OnGUI()
        {
            if (visible)
            {
                if (HighLogic.CurrentGame.Parameters.CustomParams<EL>().useKSPskin)
                    GUI.skin = HighLogic.Skin;
                win = ClickThruBlocker.GUILayoutWindow(id, win, Win, "Engine Light Relit");

            }
        }
        void Win(int id)
        {
            GUILayout.BeginVertical();
            GUILayout.Space(20);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Light Fade:");

            GUILayout.FlexibleSpace();

            var newValue = GUILayout.HorizontalSlider(HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightFadeCoefficient, 0.1f, 1.0f, GUILayout.Width(250));
            if (newValue != HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightFadeCoefficient)
            {
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightFadeCoefficient = newValue;
                ConfigChanged = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Light Power:");
            GUILayout.FlexibleSpace();

            newValue = GUILayout.HorizontalSlider(HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightPower, 0.1f, 1.0f, GUILayout.Width(250));
            if (newValue != HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightPower)
            {
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightPower = newValue;
                ConfigChanged = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Light Range:");
            GUILayout.FlexibleSpace();

            newValue = GUILayout.HorizontalSlider(HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightRange, 0.1f, 1.0f, GUILayout.Width(250));
            if (newValue != HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightRange)
            {
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightRange = newValue;
                ConfigChanged = true;
            }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Max", GUILayout.Width(90)))
            {
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightFadeCoefficient = 1f;
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightPower = 1f;
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightRange = 1f;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Low", GUILayout.Width(90)))
            {
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightFadeCoefficient = 0.4f;
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightPower = 0.1f;
                HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightRange = 0.5f;
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Close"))
                visible = false;
            GUILayout.EndVertical();
            GUI.DragWindow();
        }
    }
}
