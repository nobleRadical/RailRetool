using BepInEx;
using BepInEx.Configuration;
using EntityStates;
using R2API;
using R2API.Utils;
using RoR2;
using RoR2.Skills;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using System.Security;
using System.Security.Permissions;

[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]

namespace RailRetoolPlugin
{

    //This attribute specifies that we have a dependency on R2API, as we're using it to add our item to the game.
    //You don't need this if you're not using R2API in your plugin, it's just to tell BepInEx to initialize R2API before this plugin so it's safe to use R2API.
    [BepInDependency(R2API.R2API.PluginGUID)]
	
	//This attribute is required, and lists metadata for your plugin.
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	
	//We will be using 2 modules from R2API: ItemAPI to add our item and LanguageAPI to add our language tokens.
    [R2APISubmoduleDependency(nameof(LoadoutAPI), nameof(LanguageAPI))]
	
	//This is the main declaration of our plugin class. BepInEx searches for all classes inheriting from BaseUnityPlugin to initialize on startup.
    //BaseUnityPlugin itself inherits from MonoBehaviour, so you can use this as a reference for what you can declare and use in your plugin class: https://docs.unity3d.com/ScriptReference/MonoBehaviour.html
    public class RailRetoolPlugin : BaseUnityPlugin
	{
        //The Plugin GUID should be a unique ID for this plugin, which is human readable (as it is used in places like the config).
        //If we see this PluginGUID as it is on thunderstore, we will deprecate this mod. Change the PluginAuthor and the PluginName !
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "nobleRadical";
        public const string PluginName = "RailRetoolPlugin";
        public const string PluginVersion = "1.1.0";

		//We need our skill definition to persist through our functions, and therefore make it a class field.
        private static SkillDef mySkillDef;

        //Adding configs here
        public static ConfigEntry<bool> shouldSwitchEquipment { get; set; }
        //The Awake() method is run at the very start when the game is initialized.
        public void Awake()
        {
            //Init our logging class so that we can properly log for debugging
            Log.Init(Logger);

            mySkillDef = ScriptableObject.CreateInstance<SkillDef>();

            //load config
            shouldSwitchEquipment = Config.Bind<bool>("base", "Should Switch Equipment", true, "Should Railgunner's Retool skill switch the equipment slot.");


            On.RoR2.SurvivorCatalog.Init += (orig) =>
            {
                orig();
                registerSkill();
                ///debugGetSkills();
            };
            On.EntityStates.Railgunner.Backpack.BaseOnline.FixedUpdate += (orig, self) => 
            {
                orig(self);
                if (self.outer.nextState is EntityStates.Railgunner.Backpack.Disconnected)
                {
                    self.outer.nextState = new EntityStates.Railgunner.Backpack.OnlineCryo();
                }
            };


            // This line of log will appear in the bepinex console when the Awake method is done.
            Log.LogInfo(nameof(Awake) + " done.");

            bool registerSkill()
            {

                //Log.LogWarning(DLC1Content.Survivors.Railgunner.bodyPrefab.GetComponent<SkillLocator>().GetSkill(SkillSlot.Secondary) == DLC1Content.Survivors.Railgunner.bodyPrefab.GetComponent<SkillLocator>().secondary);
                //Grab MUL-T's retool SkillDef
                SkillDef RoR2_Retool = Addressables.LoadAssetAsync<SkillDef>("RoR2/Base/Toolbot/ToolbotBodySwap.asset").WaitForCompletion();

                // Language Tokens, check AddTokens() below.
                mySkillDef.skillName = "RailRetool";
                mySkillDef.skillNameToken = "RAILGUNNER_SPECIAL_RAILRETOOL_NAME";
                mySkillDef.skillDescriptionToken = shouldSwitchEquipment.Value ? "RAILGUNNER_SPECIAL_RAILRETOOL_DESC" : "RAILGUNNER_SPECIAL_RAILRETOOL_DESC_ALT";

                mySkillDef.activationState = new SerializableEntityStateType(typeof(RailRetoolState));
                mySkillDef.activationStateMachineName = "Weapon";
                mySkillDef.baseMaxStock = 1;
                mySkillDef.baseRechargeInterval = (float)0.5;
                mySkillDef.beginSkillCooldownOnSkillEnd = false;
                mySkillDef.canceledFromSprinting = false;
                mySkillDef.cancelSprintingOnActivation = false;
                mySkillDef.dontAllowPastMaxStocks = false;
                mySkillDef.forceSprintDuringState = false;
                mySkillDef.fullRestockOnAssign = true;
                mySkillDef.icon = RoR2_Retool.icon;
                mySkillDef.interruptPriority = RoR2_Retool.interruptPriority;
                mySkillDef.isCombatSkill = false;
                mySkillDef.mustKeyPress = false;
                mySkillDef.rechargeStock = 1;
                mySkillDef.requiredStock = 1;
                mySkillDef.resetCooldownTimerOnUse = true;
                mySkillDef.stockToConsume = 1;


                //Then finally add it to R2API
                bool success = ContentAddition.AddSkillDef(mySkillDef);
                if (!success)
                {
                    Log.LogError("Railgunner Skill Loading Failed.");
                }

                SkillLocator skillLocator = Addressables.LoadAssetAsync<SurvivorDef>("RoR2/DLC1/Railgunner/Railgunner.asset").WaitForCompletion().bodyPrefab.GetComponent<SkillLocator>();

                //Note; if your character does not originally have a skill family for this, use the following:
                //skillLocator.special = gameObject.AddComponent<GenericSkill>();
                //var newFamily = ScriptableObject.CreateInstance<SkillFamily>();
                //LoadoutAPI.AddSkillFamily(newFamily);
                //skillLocator.special.SetFieldValue("_skillFamily", newFamily);
                //var specialSkillFamily = skillLocator.special.skillFamily;


                //Note; you can change component.primary to component.secondary , component.utility and component.special
                SkillFamily skillFamily = skillLocator.special.skillFamily;

                //If this is an alternate skill, use this code.
                // Here, we add our skill as a variant to the exisiting Skill Family.
                Array.Resize(ref skillFamily.variants, skillFamily.variants.Length + 1);
                skillFamily.variants[skillFamily.variants.Length - 1] = new SkillFamily.Variant
                {
                    skillDef = mySkillDef,
                    unlockableName = "",
                    viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)

                };

                //Note; if your character does not originally have a skill family for this, use the following:
                //skillFamily.variants = new SkillFamily.Variant[1]; // substitute 1 for the number of skill variants you are implementing

                //If this is the default/first skill, copy this code and remove the //,
                //skillFamily.variants[0] = new SkillFamily.Variant
                //{
                //    skillDef = mySkillDef,
                //    unlockableName = "",
                //    viewableNode = new ViewablesCatalog.Node(mySkillDef.skillNameToken, false, null)
                //};

                AddTokens();
                return success;
            }
        }

        private void debugGetSkills()
        {
            Log.LogDebug(DLC1Content.Survivors.Railgunner.bodyPrefab.GetComponent<SkillLocator>().secondary.skillFamily.variants[0].skillDef.skillName);
        }

        //This function adds the tokens from the item using LanguageAPI, the comments in here are a style guide, but is very opiniated. Make your own judgements!
        private void AddTokens()
        {
            //The Pickup is the short text that appears when you first pick this up. This text should be short and to the point, numbers are generally ommited.
            LanguageAPI.Add("RAILGUNNER_SPECIAL_RAILRETOOL_NAME", "Retool");

            //The Description is where you put the actual numbers and give an advanced description.
            LanguageAPI.Add("RAILGUNNER_SPECIAL_RAILRETOOL_DESC", "Passively hold <style=cIsUtility>TWO equipment</style> at once. Activating 'Retool' switches the <style=cIsUtility>active Equipment</style> and <style=cIsVoid>Railgunner's</style> <style=cIsDamage>secondary attack</style>.");
            LanguageAPI.Add("RAILGUNNER_SPECIAL_RAILRETOOL_DESC_ALT", "Switches <style=cIsVoid>Railgunner's</style> <style=cIsDamage>secondary attack</style>.");
        }

        //The Update() method is run on every frame of the game.
        private void Update()
        {

        }
    }
    public class RailRetoolState : BaseSkillState
    {
        private string soundString = "RoR2/DLC1/Railgunner/lsdRailgunnerReload";

        protected EntityState InstantiateBackpackState()
        {
            return new EntityStates.Railgunner.Backpack.OnlineSuper();
        }
        public override void OnEnter()
        {
            base.OnEnter();
            Util.PlaySound(soundString, base.gameObject);

            if (skillLocator.secondary.skillDef.skillName == "ScopeHeavy") // if we're currently on ScopeHeavy (default skill)
            {
                ///Log.LogDebug("Switching Railgunner skills: On");
                skillLocator.secondary.AssignSkill(skillLocator.secondary.skillFamily.variants[1].skillDef);
                if (RailRetoolPlugin.shouldSwitchEquipment.Value)
                {
                    characterBody.inventory.SetActiveEquipmentSlot(1); // set to alternate equipment if enabled
                }
            }
            else if (skillLocator.secondary.skillDef.skillName == "ScopeLight") // if we're currently on ScopeLight (alt skill)
            {
                ///Log.LogDebug("Switching Railgunner skills: Off");
                skillLocator.secondary.AssignSkill(skillLocator.secondary.skillFamily.variants[0].skillDef);
                if (RailRetoolPlugin.shouldSwitchEquipment.Value)
                {
                    characterBody.inventory.SetActiveEquipmentSlot(0); // set to alternate equipment if enabled
                }
            }
            else // idk what would be happening here (another mod?) but we're not going to touch that for now.
            {
                Log.LogWarning("Unknown skill detected. Doing Nothing.");
            }

            //this.characterBody.equipmentSlot
            outer.SetNextStateToMain();
        }
    }
}
