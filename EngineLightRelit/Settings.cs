using System.Collections;
using System.Reflection;


namespace EngineLightRelit
{
    // http://forum.kerbalspaceprogram.com/index.php?/topic/147576-modders-notes-for-ksp-12/#comment-2754813
    // search for "Mod integration into Stock Settings
    //
    // HighLogic.CurrentGame.Parameters.CustomParams<EL>()

    public class EL : GameParameters.CustomParameterNode
    {
        public override string Title { get { return ""; } }
        public override GameParameters.GameMode GameMode { get { return GameParameters.GameMode.ANY; } }
        public override string Section { get { return "Engine Light Relit"; } }
        public override string DisplaySection { get { return "Engine Light Relit"; } }
        public override int SectionOrder { get { return 1; } }
        public override bool HasPresets { get { return false; } }

       
        [GameParameters.CustomFloatParameterUI("Light Fade Coefficient ", displayFormat = "N2", minValue = 0.1f, maxValue = 1f, stepCount = 91, asPercentage = false,
            toolTip = "This reduces the affect of the lightFadeCoefficient in the default settings")]
        public float lightFadeCoefficient = 1.0f;

        [GameParameters.CustomFloatParameterUI("Light Power", displayFormat = "N2", minValue = 0.1f, maxValue = 1f, stepCount = 91, asPercentage = false,
            toolTip = "This reduces the affect of the lightPower in the default settings")]
        public float lightPower = 1f;

        [GameParameters.CustomFloatParameterUI("Light Range", displayFormat = "N2", minValue = 0.1f, maxValue = 1f, stepCount = 91, asPercentage = false,
            toolTip = "This reduces the affect of the lightRange in the default settings")]
        public float lightRange = 1f;

        [GameParameters.CustomParameterUI("Use KSP skin")]
        public bool useKSPskin = false;


        public override bool Interactible(MemberInfo member, GameParameters parameters)
        {
            return true;
        }
        public override IList ValidValues(MemberInfo member)
        {
            return null;
        }
    }
}
