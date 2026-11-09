using FIMSpace.FEditor;
using UnityEditor;
using UnityEngine;

namespace FIMSpace.FProceduralAnimation
{

    public partial class LegsAnimatorEditor : Editor
    {
        [HideInInspector] public Object ModulesDirectory;
        [HideInInspector] public Object MotionPresetsDirectory;
        [HideInInspector] public Object ImpulsesDirectory;
        [HideInInspector] public Object DemosPackage;
        [HideInInspector] public Object UserManualFile;
        [HideInInspector] public Object AssemblyDefinitions;
        [HideInInspector] public Object AssemblyDefinitionsAll;


        [System.NonSerialized] public SerializedProperty sp_Debug_IsGrounded;
        [System.NonSerialized] public SerializedProperty sp_BaseTransform;
        [System.NonSerialized] public SerializedProperty sp_LegsAnimatorBlend;
        [System.NonSerialized] public SerializedProperty sp_Hips;
        [System.NonSerialized] public SerializedProperty sp_HipsChildSpineBone;
        [System.NonSerialized] public SerializedProperty sp_DelayedInitialization;
        [System.NonSerialized] public SerializedProperty sp_Legs;
        [System.NonSerialized] public SerializedProperty sp_ImpulsesPowerMultiplier;
        //public SerializedProperty sp_LegsMainSettings;
        [System.NonSerialized] public SerializedProperty sp_AnimateFoot;
        [System.NonSerialized] public SerializedProperty sp_ScRefMode;
        [System.NonSerialized] public SerializedProperty sp_customScaleReferenceValue;
        [System.NonSerialized] public SerializedProperty sp_GroundMask;
        [System.NonSerialized] public SerializedProperty sp_IKHint;
        [System.NonSerialized] public SerializedProperty sp_CastDistance;
        [System.NonSerialized] public SerializedProperty sp_MaxStepDown;
        [System.NonSerialized] public SerializedProperty sp_StabilityAlgorithm;
        [System.NonSerialized] public SerializedProperty sp_UseHipsRotation;
        [System.NonSerialized] public SerializedProperty sp_Event_OnStep;
        [System.NonSerialized] public SerializedProperty sp_ExtraPelvisOffset;
        [System.NonSerialized] public SerializedProperty sp_Mecanim;
        [System.NonSerialized] public SerializedProperty sp_HipsSetup;
        [System.NonSerialized] public SerializedProperty sp_BaseLegAnimating;
        [System.NonSerialized] public SerializedProperty sp_SmoothSuddenSteps;
        [System.NonSerialized] public SerializedProperty sp_HipsAdjustingBlend;
        [System.NonSerialized] public SerializedProperty sp_GlueBlend;
        [System.NonSerialized] public SerializedProperty sp_GlueMode;
        [System.NonSerialized] public SerializedProperty sp_MotionInfluence;
        [System.NonSerialized] public SerializedProperty sp_StepPointsOverlapRadius;

        [System.NonSerialized] public SerializedProperty sp_DisableIfInvisible;
        [System.NonSerialized] public SerializedProperty sp_DisableIfInvisibleArray;
        [System.NonSerialized] public SerializedProperty sp_FadeOffAtDistance;
        [System.NonSerialized] public SerializedProperty sp_SwingHelper;
        [System.NonSerialized] public SerializedProperty sp_AnimationFloorLevel;
        [System.NonSerialized] public SerializedProperty sp_ExtraHipsHubs;
        [System.NonSerialized] public SerializedProperty sp__StepHeatPenaltyCurve;
        [System.NonSerialized] public SerializedProperty sp_RagdolledParameter;

        [System.NonSerialized] public SerializedProperty sp_Rigidbody;
        [System.NonSerialized] public SerializedProperty sp_Calibrate;

        protected virtual void OnEnable()
        {
            Get.Legs_RefreshLegsOwner();
            Get.DoBackCompatibilityChecks();

            sp_Debug_IsGrounded = serializedObject.FindProperty("Debug_IsGrounded");
            sp_BaseTransform = serializedObject.FindProperty("baseTransform");
            sp_LegsAnimatorBlend = serializedObject.FindProperty("LegsAnimatorBlend");

            sp_Hips = serializedObject.FindProperty("Hips");
            sp_HipsChildSpineBone = serializedObject.FindProperty("HipsChildSpineBone");
            sp_DelayedInitialization = serializedObject.FindProperty("DelayedInitialization");
            sp_Legs = serializedObject.FindProperty("Legs");
            sp_ImpulsesPowerMultiplier = serializedObject.FindProperty("ImpulsesPowerMultiplier");
            sp_AnimateFoot = serializedObject.FindProperty("AnimateFeet");
            //sp_LegsMainSettings = serializedObject.FindProperty("LegsMainSettings");
            sp_ScRefMode = serializedObject.FindProperty("ScaleReferenceMode");
            sp_customScaleReferenceValue = serializedObject.FindProperty("customScaleReferenceValue");
            sp_GroundMask = serializedObject.FindProperty("GroundMask");
            sp_IKHint = serializedObject.FindProperty("IKHintMode");
            sp_CastDistance = serializedObject.FindProperty("CastDistance");
            sp_MaxStepDown = serializedObject.FindProperty( "BodyStepDown" );
            sp_StabilityAlgorithm = serializedObject.FindProperty("StabilityAlgorithm");
            sp_UseHipsRotation = serializedObject.FindProperty("UseHipsRotation");
            sp_Event_OnStep = serializedObject.FindProperty("Event_OnStep");
            sp_ExtraPelvisOffset = serializedObject.FindProperty("ExtraPelvisOffset");
            sp_Mecanim = serializedObject.FindProperty("Mecanim");
            sp_HipsSetup = serializedObject.FindProperty("HipsSetup");
            sp_BaseLegAnimating = serializedObject.FindProperty("BaseLegAnimating");
            sp_SmoothSuddenSteps = serializedObject.FindProperty("SmoothSuddenSteps");
            sp_HipsAdjustingBlend = serializedObject.FindProperty("UseHips");
            //sp_HipsAdjustingBlend = serializedObject.FindProperty("HipsAdjustingBlend");
            sp_GlueBlend = serializedObject.FindProperty("UseGluing");
            //sp_GlueBlend = serializedObject.FindProperty("GlueBlend");
            sp_GlueMode = serializedObject.FindProperty("GlueMode");
            sp_MotionInfluence = serializedObject.FindProperty("MotionInfluence");
            sp_StepPointsOverlapRadius = serializedObject.FindProperty("StepPointsOverlapRadius");

            sp_DisableIfInvisible = serializedObject.FindProperty("DisableIfInvisible");
            sp_DisableIfInvisibleArray = serializedObject.FindProperty("DisableIfInvisibleExtraRenderers");
            sp_FadeOffAtDistance = serializedObject.FindProperty("FadeOffAtDistance");
            sp_SwingHelper = serializedObject.FindProperty("SwingHelper");
            sp_AnimationFloorLevel = serializedObject.FindProperty("AnimationFloorLevel");
            sp_ExtraHipsHubs = serializedObject.FindProperty("ExtraHipsHubs");
            sp__StepHeatPenaltyCurve = serializedObject.FindProperty("_StepHeatPenaltyCurve");
            sp_RagdolledParameter = serializedObject.FindProperty("RagdolledParameter");

            sp_Calibrate = serializedObject.FindProperty("Calibrate");
            sp_Rigidbody = serializedObject.FindProperty("Rigidbody");


            OnChange(false);
        }

        SerializedProperty GetLegSerializedProperty(int leg)
        {
            if (leg < 0) return null;
            if (leg >= Get.Legs.Count) return null;
            return sp_Legs.GetArrayElementAtIndex(leg);
        }



        protected virtual void OnChange(bool dirty = true)
        {
            if ( dirty) EditorUtility.SetDirty(Get);
            _perf_lastMin = long.MaxValue;
            _perf_lastMax = long.MinValue;
            _perf_totalSteps = 0;
        }

    }

}