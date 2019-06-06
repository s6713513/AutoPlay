using BepInEx;
using KinematicCharacterController;
using Rewired;
using RoR2;
using RoR2.Networking;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace AutoPlay
{ 
    [BepInPlugin("com.qc.auto", "Auto", "1.5")]
    public class ClientCheat : BaseUnityPlugin
    {
        //杀死其他友方角色信息
        private List<DamageDealtMessage> message;
        private static ClientCheat instance;
        private bool locked;
        public readonly float KILL_DAMAGE = 912314368;
        //是否考虑队友捡物品
        private bool friendly;
        //是否完成瞬移
        private bool isTransport;
        //可以自动拾取的物品数量
        private float purchaseChest;       
        //摸物体间隔时间
        private float interactTime;
        //是否完成设定目标（拾取一定数量装备）
        private bool hasAccomplish;
        //是否完成计算总拾取数量
        private bool hasCalculate;
        //BOSS击杀后需要额外捡取的物品数量
        private float defeatedNum;
        //场景中有些物体没有identity，会出现警告，用此list标记，以免无限刷警告
        private List<NetworkInstanceId> networkInstances;
        //场景中是否存在天堂传送门
        private bool hasCele;
        //山之挑战层数（由于客机拿不到数据，需要自行统计）
        private int buffMountains;
        //是否加载完场景绝大部分物体（防止正在刷怪的时候又杀怪）
        private bool isLoad;

        CancellationTokenSource cancelToken;
        Task taskKillMonster = new Task(()=> { });
        Task taskPickUp = new Task(() => { });
        Task taskTeleporter = new Task(() => { });

        private Dictionary<GameObject, NetworkInstanceId> myChest;
        private Vector3 position;
        private Quaternion rotation;
        #region 要修改的人物属性
        private int jumpCount = 20;
        private float attackSpeedNum = 10;
        private float critNum = 10;
        private float moveSpeedNum = 3;
        private float damageNum = 1.8f;
        private float armorNum = 3000;
        #endregion

        public NetworkUser otherPlayer { get; private set; }
        public NetworkUser localPlayer { get; private set; }

        public ClientCheat()
        {
            message = new List<DamageDealtMessage>();
            locked = false;
            ClientCheat.instance = this;
            cancelToken = new CancellationTokenSource();
            myChest = new Dictionary<GameObject, NetworkInstanceId>();
            networkInstances = new List<NetworkInstanceId>();
            InitData();            
            isLoad = false;
            taskKillMonster.Start();
            taskPickUp.Start();
            taskTeleporter.Start();
            On.RoR2.Chat.AddPickupMessage += Chat_AddPickupMessage;
            On.RoR2.Chat.AddMessage_ChatMessageBase += Chat_AddMessage_ChatMessageBase;
            On.RoR2.Networking.GameNetworkManager.OnClientSceneChanged += GameNetworkManager_OnClientSceneChanged;
            new KeyCodeEvent();
        }

        private void GameNetworkManager_OnClientSceneChanged(On.RoR2.Networking.GameNetworkManager.orig_OnClientSceneChanged orig, GameNetworkManager self, NetworkConnection conn)
        {
            //isLoad = true;
            cancelToken = new CancellationTokenSource();
            orig(self, conn);
        }

        private void Chat_AddMessage_ChatMessageBase(On.RoR2.Chat.orig_AddMessage_ChatMessageBase orig, Chat.ChatMessageBase message)
        {
            string text = message.ConstructChatString();
            if(text.Contains("invited the challenge of the Mountain") && !text.Contains(":"))
            {
                buffMountains++;                
            }
            orig(message);
        }

        private void Chat_AddPickupMessage(On.RoR2.Chat.orig_AddPickupMessage orig, CharacterBody body, string pickupToken, Color32 pickupColor, uint pickupQuantity)
        {
            if (CanDoOrNot() && body == localPlayer.GetCurrentBody() && !pickupColor.Equals(ColorCatalog.GetColor(ColorCatalog.ColorIndex.LunarItem))
                && !pickupColor.Equals(ColorCatalog.GetColor(ColorCatalog.ColorIndex.Equipment)))
            {
                if (purchaseChest > 0)
                {
                    purchaseChest--;
                }
            }
            orig(body, pickupToken, pickupColor, pickupQuantity);
        }


        public static ClientCheat GetInstance()
        {
            return instance;
        }

        public void Enable()
        {
            On.RoR2.GlobalEventManager.ClientDamageNotified += ClientDamageNotified;
            On.RoR2.CameraRigController.Update += CameraRigController_Update;
            message.Clear();
            locked = false;
        }

        public void ModifyCharacterAttr()
        {
            jumpCount = 20;
            attackSpeedNum = 10;
            critNum = 10;
            moveSpeedNum = 3;
            damageNum = 1.8f;
            armorNum = 3000;
            On.RoR2.CharacterBody.RecalculateStats += CharacterBody_RecalculateStats;
        }

        public void ResetCharacterAttr()
        {
            jumpCount = 0;
            attackSpeedNum = 1;
            critNum = 1;
            moveSpeedNum = 1;
            damageNum = 1;
            armorNum = 0;
            //On.RoR2.CharacterBody.RecalculateStats -= CharacterBody_RecalculateStats;
        }

        //人物属性修改(客机血量相关修改无效)
        private void CharacterBody_RecalculateStats(On.RoR2.CharacterBody.orig_RecalculateStats orig, CharacterBody self)
        {            
            if (NetworkUser.readOnlyInstancesList.Count != 0 && self)
            {
                foreach (NetworkUser networkUser in NetworkUser.readOnlyInstancesList)
                {
                    if (networkUser.isLocalPlayer && networkUser.GetCurrentBody() == self)
                    {
                        #region 修改人物属性
                        CommonUtil.SetNotPublicPro(self, "experience", TeamManager.instance.GetTeamExperience(self.teamComponent.teamIndex));
                        CommonUtil.SetNotPublicPro(self, "level", TeamManager.instance.GetTeamLevel(self.teamComponent.teamIndex));
                        int num = 0;
                        int num2 = 0;
                        int num3 = 0;
                        int num4 = 0;
                        int num5 = 0;
                        int num6 = 0;
                        int num7 = 0;
                        int num8 = 0;
                        int num9 = 0;
                        int num10 = 0;
                        int num11 = 0;
                        int num12 = 0;
                        int num13 = 0;
                        int num14 = 0;
                        int num15 = 0;
                        int num16 = 0;
                        int num17 = 0;
                        int num18 = 0;
                        int bonusStockFromBody = 0;
                        int num19 = 0;
                        int num20 = 0;
                        int num21 = 0;
                        int num22 = 0;
                        float num23 = 1f;
                        EquipmentIndex equipmentIndex = EquipmentIndex.None;
                        uint num24 = 0u;
                        if (self.inventory)
                        {
                            CommonUtil.SetNotPublicPro(self, "level", (float)self.inventory.GetItemCount(ItemIndex.LevelBonus) + self.level);
                            num = self.inventory.GetItemCount(ItemIndex.Infusion);
                            num2 = self.inventory.GetItemCount(ItemIndex.HealWhileSafe);
                            num3 = self.inventory.GetItemCount(ItemIndex.PersonalShield);
                            num4 = self.inventory.GetItemCount(ItemIndex.Hoof);
                            num5 = self.inventory.GetItemCount(ItemIndex.SprintOutOfCombat);
                            num6 = self.inventory.GetItemCount(ItemIndex.Feather);
                            num7 = self.inventory.GetItemCount(ItemIndex.Syringe);
                            num8 = self.inventory.GetItemCount(ItemIndex.CritGlasses);
                            num9 = self.inventory.GetItemCount(ItemIndex.AttackSpeedOnCrit);
                            num10 = self.inventory.GetItemCount(ItemIndex.CooldownOnCrit);
                            num11 = self.inventory.GetItemCount(ItemIndex.HealOnCrit);
                            num12 = self.GetBuffCount(BuffIndex.BeetleJuice);
                            num13 = self.inventory.GetItemCount(ItemIndex.ShieldOnly);
                            num14 = self.inventory.GetItemCount(ItemIndex.AlienHead);
                            num15 = self.inventory.GetItemCount(ItemIndex.Knurl);
                            num16 = self.inventory.GetItemCount(ItemIndex.BoostHp);
                            num17 = self.inventory.GetItemCount(ItemIndex.CritHeal);
                            num18 = self.inventory.GetItemCount(ItemIndex.SprintBonus);
                            bonusStockFromBody = self.inventory.GetItemCount(ItemIndex.SecondarySkillMagazine);
                            num19 = self.inventory.GetItemCount(ItemIndex.SprintArmor);
                            num20 = self.inventory.GetItemCount(ItemIndex.UtilitySkillMagazine);
                            num21 = self.inventory.GetItemCount(ItemIndex.HealthDecay);
                            num23 = self.CalcLunarDaggerPower();
                            equipmentIndex = self.inventory.currentEquipmentIndex;
                            num24 = self.inventory.infusionBonus;
                            num22 = self.inventory.GetItemCount(ItemIndex.DrizzlePlayerHelper);
                        }
                        float num25 = self.level - 1f;
                        BuffMask buffMask = (BuffMask)CommonUtil.GetNotPublicVar(self, "buffMask");
                        CommonUtil.SetNotPublicPro(self, "isElite", buffMask.containsEliteBuff);
                        float maxHealth = self.maxHealth;
                        float maxShield = self.maxShield;
                        float num26 = self.baseMaxHealth + self.levelMaxHealth * num25;
                        float num27 = 1f;
                        num27 += (float)num16 * 0.1f;
                        if (num > 0)
                        {
                            num26 += num24;
                        }
                        num26 += (float)num15 * 40f;
                        num26 *= num27;
                        num26 /= num23;
                        CommonUtil.SetNotPublicPro(self, "maxHealth", num26);
                        float num28 = self.baseRegen + self.levelRegen * num25;
                        num28 *= 2.5f;
                        if (self.outOfDanger && num2 > 0)
                        {
                            num28 *= 2.5f + (float)(num2 - 1) * 1.5f;
                        }
                        num28 += (float)num15 * 1.6f;
                        if (num21 > 0)
                        {
                            num28 -= self.maxHealth / (float)num21;
                        }
                        CommonUtil.SetNotPublicPro(self, "regen", num28);

                        float num29 = self.baseMaxShield + self.levelMaxShield * num25;
                        num29 += (float)num3 * 25f;
                        if (self.HasBuff(BuffIndex.EngiShield))
                        {
                            num29 += self.maxHealth * 1f;
                        }
                        if (self.HasBuff(BuffIndex.EngiTeamShield))
                        {
                            num29 += self.maxHealth * 0.5f;
                        }
                        if (num13 > 0)
                        {
                            num29 += self.maxHealth * (1.5f + (float)(num13 - 1) * 0.25f);
                            CommonUtil.SetNotPublicPro(self, "maxHealth", 1f);

                        }
                        if (buffMask.HasBuff(BuffIndex.AffixBlue))
                        {
                            float num30 = self.maxHealth * 0.5f;
                            CommonUtil.SetNotPublicPro(self, "maxHealth", self.maxHealth - num30);

                            num29 += self.maxHealth;
                        }
                        CommonUtil.SetNotPublicPro(self, "maxShield", num29);

                        float num31 = self.baseMoveSpeed + self.levelMoveSpeed * num25;
                        float num32 = 1f;
                        if (Run.instance.enabledArtifacts.HasArtifact(ArtifactIndex.Spirit))
                        {
                            float num33 = 1f;
                            if (self.healthComponent)
                            {
                                num33 = self.healthComponent.combinedHealthFraction;
                            }
                            num32 += 1f - num33;
                        }
                        if (equipmentIndex == EquipmentIndex.AffixYellow)
                        {
                            num31 += 2f;
                        }

                        if (self.isSprinting)
                        {
                            float sprintingSpeedMultiplier = (float)CommonUtil.GetNotPublicVar(self, "sprintingSpeedMultiplier");
                            CommonUtil.SetNotPublicVar(self, "sprintingSpeedMultiplier", sprintingSpeedMultiplier * num31);

                        }
                        if (self.outOfCombat && self.outOfDanger && num5 > 0)
                        {
                            num32 += (float)num5 * 0.3f;
                        }
                        num32 += (float)num4 * 0.14f;
                        if (self.isSprinting && num18 > 0)
                        {
                            float sprintingSpeedMultiplier = (float)CommonUtil.GetNotPublicVar(self, "sprintingSpeedMultiplier");
                            num32 += (0.1f + 0.2f * (float)num18) / sprintingSpeedMultiplier;
                        }
                        if (self.HasBuff(BuffIndex.BugWings))
                        {
                            num32 += 0.2f;
                        }
                        if (self.HasBuff(BuffIndex.Warbanner))
                        {
                            num32 += 0.3f;
                        }
                        if (self.HasBuff(BuffIndex.EnrageAncientWisp))
                        {
                            num32 += 0.4f;
                        }
                        if (self.HasBuff(BuffIndex.CloakSpeed))
                        {
                            num32 += 0.4f;
                        }
                        if (self.HasBuff(BuffIndex.TempestSpeed))
                        {
                            num32 += 1f;
                        }
                        if (self.HasBuff(BuffIndex.WarCryBuff))
                        {
                            num32 += 0.5f;
                        }
                        if (self.HasBuff(BuffIndex.EngiTeamShield))
                        {
                            num32 += 0.3f;
                        }
                        float num34 = 1f;
                        if (self.HasBuff(BuffIndex.Slow50))
                        {
                            num34 += 0.5f;
                        }
                        if (self.HasBuff(BuffIndex.Slow60))
                        {
                            num34 += 0.6f;
                        }
                        if (self.HasBuff(BuffIndex.Slow80))
                        {
                            num34 += 0.8f;
                        }
                        if (self.HasBuff(BuffIndex.ClayGoo))
                        {
                            num34 += 0.5f;
                        }
                        if (self.HasBuff(BuffIndex.Slow30))
                        {
                            num34 += 0.3f;
                        }
                        if (self.HasBuff(BuffIndex.Cripple))
                        {
                            num34 += 1f;
                        }

                        num31 *= num32 / num34;
                        if (num12 > 0)
                        {
                            num31 *= 1f - 0.05f * (float)num12;
                        }
                        if (num31 * moveSpeedNum <= 20)
                        {
                            CommonUtil.SetNotPublicPro(self, "moveSpeed", num31 * moveSpeedNum);
                        }
                        else
                        {
                            CommonUtil.SetNotPublicPro(self, "moveSpeed", (uint)20);
                        }
                        CommonUtil.SetNotPublicPro(self, "acceleration", self.moveSpeed / self.baseMoveSpeed * self.baseAcceleration);
                        float jumpPower = self.baseJumpPower + self.levelJumpPower * num25;
                        CommonUtil.SetNotPublicPro(self, "jumpPower", jumpPower);
                        CommonUtil.SetNotPublicPro(self, "maxJumpHeight", Trajectory.CalculateApex(self.jumpPower));
                        CommonUtil.SetNotPublicPro(self, "maxJumpCount", self.baseJumpCount + num6 + jumpCount);
                        float num35 = self.baseDamage + self.levelDamage * num25;
                        float num36 = 1f;
                        int num37 = self.inventory ? self.inventory.GetItemCount(ItemIndex.BoostDamage) : 0;
                        if (num37 > 0)
                        {
                            num36 += (float)num37 * 0.1f;
                        }
                        if (num12 > 0)
                        {
                            num36 -= 0.05f * (float)num12;
                        }
                        if (self.HasBuff(BuffIndex.GoldEmpowered))
                        {
                            num36 += 1f;
                        }
                        num36 += num23 - 1f;
                        num35 *= num36;
                        CommonUtil.SetNotPublicPro(self, "damage", num35 * damageNum);
                        float num38 = self.baseAttackSpeed + self.levelAttackSpeed * num25;
                        float num39 = 1f;
                        num39 += (float)num7 * 0.15f;
                        if (equipmentIndex == EquipmentIndex.AffixYellow)
                        {
                            num39 += 0.5f;
                        }
                        int[] buffs = (int[])CommonUtil.GetNotPublicVar(self, "buffs");
                        num39 += (float)buffs[2] * 0.12f;
                        if (self.HasBuff(BuffIndex.Warbanner))
                        {
                            num39 += 0.3f;
                        }
                        if (self.HasBuff(BuffIndex.EnrageAncientWisp))
                        {
                            num39 += 2f;
                        }
                        if (self.HasBuff(BuffIndex.WarCryBuff))
                        {
                            num39 += 1f;
                        }
                        num38 *= num39;
                        if (num12 > 0)
                        {
                            num38 *= 1f - 0.05f * (float)num12;
                        }

                        CommonUtil.SetNotPublicPro(self, "attackSpeed", num38 * attackSpeedNum);

                        float num40 = self.baseCrit + self.levelCrit * num25;
                        num40 += (float)num8 * 10f;
                        if (num9 > 0)
                        {
                            num40 += 5f;
                        }
                        if (num10 > 0)
                        {
                            num40 += 5f;
                        }
                        if (num11 > 0)
                        {
                            num40 += 5f;
                        }
                        if (num17 > 0)
                        {
                            num40 += 5f;
                        }
                        if (self.HasBuff(BuffIndex.FullCrit))
                        {
                            num40 += 100f;
                        }
                        CommonUtil.SetNotPublicPro(self, "crit", num40 * critNum);
                        CommonUtil.SetNotPublicPro(self, "armor", self.baseArmor + self.levelArmor * num25 + (self.HasBuff(BuffIndex.ArmorBoost) ? 200f : 0f) + (float)num22 * 70f + armorNum);
                        if (self.HasBuff(BuffIndex.Cripple))
                        {
                            CommonUtil.SetNotPublicPro(self, "armor", self.armor - 20);
                        }
                        if (self.isSprinting && num19 > 0)
                        {
                            CommonUtil.SetNotPublicPro(self, "armor", self.armor + (float)(num19 * 30));
                        }
                        float num41 = 1f;
                        if (self.HasBuff(BuffIndex.GoldEmpowered))
                        {
                            num41 *= 0.25f;
                        }
                        for (int i = 0; i < num14; i++)
                        {
                            num41 *= 0.75f;
                        }
                        if (self.HasBuff(BuffIndex.NoCooldowns))
                        {
                            num41 = 0f;
                        }
                        SkillLocator skillLocator = (SkillLocator)CommonUtil.GetNotPublicVar(self, "skillLocator");
                        if (skillLocator.primary)
                        {
                            skillLocator.primary.cooldownScale = num41;
                        }
                        if (skillLocator.secondary)
                        {
                            skillLocator.secondary.cooldownScale = num41;
                            skillLocator.secondary.SetBonusStockFromBody(bonusStockFromBody);
                        }
                        if (skillLocator.utility)
                        {
                            float num42 = num41;
                            if (num20 > 0)
                            {
                                num42 *= 0.6666667f;
                            }
                            skillLocator.utility.cooldownScale = num42;
                            skillLocator.utility.SetBonusStockFromBody(num20 * 2);
                        }
                        if (skillLocator.special)
                        {
                            skillLocator.special.cooldownScale = num41;
                        }
                        CommonUtil.SetNotPublicPro(self, "critHeal", 0f);
                        if (num17 > 0)
                        {
                            float crit = self.crit;
                            CommonUtil.SetNotPublicPro(self, "crit", self.crit * critNum / (float)(num17 + 1));
                            CommonUtil.SetNotPublicPro(self, "critHeal", crit - self.crit);
                        }
                        if (NetworkServer.active)
                        {
                            float num43 = self.maxHealth - maxHealth;
                            float num44 = self.maxShield - maxShield;
                            if (num43 > 0f)
                            {
                                self.healthComponent.Heal(num43, default(ProcChainMask), false);
                            }
                            else if (self.healthComponent.health > self.maxHealth)
                            {
                                self.healthComponent.Networkhealth = self.maxHealth;
                            }
                            if (num44 > 0f)
                            {
                                self.healthComponent.RechargeShield(num44);
                            }
                        }
                        CommonUtil.SetNotPublicVar(self, "statsDirty", false);
                        #endregion
                        return;
                    }
                }
            }            
            orig(self);
        }

        /*无技能cd*/
        private void CameraRigController_Update(On.RoR2.CameraRigController.orig_Update orig, CameraRigController self)
        {
            #region 重置自己的技能cd
            if (NetworkUser.readOnlyLocalPlayersList.Count != 0 && NetworkUser.readOnlyLocalPlayersList[0].isLocalPlayer)
            {
                Player player = NetworkUser.readOnlyLocalPlayersList[0].inputPlayer;
                if (player != null)
                {
                    if (player.GetAnyButtonDown())
                    {
                        CharacterBody body = NetworkUser.readOnlyLocalPlayersList[0].GetCurrentBody();
                        if (body)
                        {
                            SkillLocator component = body.GetComponent<SkillLocator>();
                            float dt = 999;
                            if (component.primary)
                            {
                                component.primary.RunRecharge(dt);
                            }
                            if (component.secondary)
                            {
                                component.secondary.RunRecharge(dt);
                            }
                            if (component.utility)
                            {
                                component.utility.RunRecharge(dt);
                            }
                            if (component.special)
                            {
                                component.special.RunRecharge(dt);
                            }
                        }
                    }
                }
            }
            #endregion
            orig(self);
        }


        public void Disable()
        {
            On.RoR2.GlobalEventManager.ClientDamageNotified -= ClientDamageNotified;
            On.RoR2.CameraRigController.Update -= CameraRigController_Update;
        }

        //重生(仅主机)
        public void Respawn()
        {
            foreach (PlayerCharacterMasterController controller in PlayerCharacterMasterController.instances)
            {
                if (controller.networkUser.isLocalPlayer)
                {
                    CharacterMaster master = controller.master;
                    if (master)
                    {
                        CommonUtil.SetNotPublicVar(master, "preventRespawnUntilNextStageServer", false);
                        CommonUtil.SetNotPublicPro(controller, "master", master);
                        CommonUtil.SetNotPublicVar(controller.master.GetBody(), "linkedToMaster", false);
                        CommonUtil.SetNotPublicVar(controller.master.GetBody(), "statsDirty", true);
                        Chat.AddMessage(controller.networkUser.userName);
                        master.CallCmdRespawn(master.bodyPrefab.name);
                    }
                }
            }
        }

        //无敌(仅主机)
        public void God_Mode()
        {
            CharacterMaster mine = null;
            ReadOnlyCollection<NetworkUser> readOnlyLocalPlayersList = NetworkUser.readOnlyLocalPlayersList;

            foreach (NetworkUser network in readOnlyLocalPlayersList)
            {
                if (network.isLocalPlayer)
                {
                    mine = network.masterObject.GetComponent<CharacterMaster>();
                    if (mine)
                    {
                        network.GetCurrentBody().healthComponent.godMode = true;
                    }
                }
            }
        }
        

        //接受所有玩家信息
        private void ClientDamageNotified(On.RoR2.GlobalEventManager.orig_ClientDamageNotified orig, DamageDealtMessage damageDealtMessage)
        {
            #region 客户端接收信息并处理
            if (!locked)
            {
                try
                {
                    locked = true;
                    //CharacterMaster mine = null;
                    //不处理当受害者已经销毁或者为罐子等没有body的物体
                    //if (damageDealtMessage.victim && damageDealtMessage.victim.GetComponent<CharacterBody>())
                    //{
                    //    //获取受害者信息
                    //    CharacterMaster victim = damageDealtMessage.victim.GetComponent<CharacterBody>().master;
                    //    HealthComponent victimHP = damageDealtMessage.victim.GetComponent<HealthComponent>();
                    //    //获取自己信息
                    //    ReadOnlyCollection<NetworkUser> readOnlyLocalPlayersList = NetworkUser.readOnlyLocalPlayersList;
                    //    mine = readOnlyLocalPlayersList[0].masterObject.GetComponent<CharacterMaster>();

                    //    //如果没有信息就随便存一条受害者是其他存活友方的(排除没有body、master、healthComponent的)
                    //    if (mine && damageDealtMessage.victim.GetComponent<CharacterBody>()
                    //        && victim != mine && victimHP && victimHP.alive
                    //        && victim.teamIndex == TeamIndex.Player)
                    //    {
                    //        if (message.Count == 0)
                    //        {
                    //            message.Add(damageDealtMessage);
                    //            Chat.AddMessage(message.Count.ToString());
                    //        }
                    //        else
                    //        {
                    //            int count = 0;
                    //            for (int i = message.Count - 1; i >= 0; i--)
                    //            {
                    //                //如果已经存在该受害者信息则不保存
                    //                if (!message[i].victim || message[i].victim.GetComponent<CharacterBody>().master == victim)
                    //                {
                    //                    continue;
                    //                }
                    //                count++;
                    //                if (count == message.Count)
                    //                {
                    //                    message.Add(damageDealtMessage);
                    //                    //Chat.AddMessage(Util.LookUpBodyNetworkUser(damageDealtMessage.victim).hasAuthority.ToString());
                    //                    Chat.AddMessage(message.Count.ToString());
                    //                }
                    //            }
                    //        }
                    //    }

                        //打开功能后
                        if (KeyCodeEvent.maxDamage)
                        {
                            //当攻击者是自己才发送信息,排除攻击者为空（比如摔掉血）情况
                            if (CanDoOrNot() && damageDealtMessage.attacker && damageDealtMessage.attacker.GetComponent<CharacterBody>()
                                && localPlayer.GetCurrentBody() == damageDealtMessage.attacker.GetComponent<CharacterBody>())
                            {
                                SendFakeMessage(CreateFakeDamage(damageDealtMessage, KILL_DAMAGE), damageDealtMessage);
                            }
                        }

                        locked = false;

                    
                }
                catch (Exception)
                {
                    locked = false;
                }
            }
            #endregion
            orig(damageDealtMessage);
        }

        //杀死除自己以外的所有玩家
        public void KillAllPlayer()
        {
            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
            {
                if (player.networkUser && !player.networkUser.isLocalPlayer && player.networkUser.GetCurrentBody() &&
                    player.networkUser.GetCurrentBody().healthComponent
                    && player.networkUser.GetCurrentBody().healthComponent.alive)
                {
                    DamageDealtMessage dealtMessage = new DamageDealtMessage();
                    dealtMessage.victim = player.networkUser.GetCurrentBody().gameObject;
                    dealtMessage.attacker = player.networkUser.GetCurrentBody().gameObject;
                    dealtMessage.position = Vector3.zero;
                    for (int j = 0; j <= 1; j++)
                    {
                        SendFakeMessage(CreateFakeDamage(dealtMessage, KILL_DAMAGE), dealtMessage);
                    }
                }
            }

            #region 杀死消息队列里的角色（暂不启用）
            //for (int i = message.Count - 1; i >= 0; i--)
            //{
            //    //如果受害者不存在或已经死亡，移除出信息列表
            //    if(!message[i].victim || !message[i].victim.GetComponent<HealthComponent>().alive)
            //    {
            //        message.Remove(message[i]);
            //    }
            //}

            //for (int i = message.Count - 1; i >= 0; i--)
            //{
            //    DamageInfo damageInfo = CreateFakeDamage(message[i], KILL_DAMAGE);
            //    CharacterBody body = message[i].victim.GetComponent<CharacterBody>();
            //    //因为官方有玩家不能被秒杀设定，所以多发送一次伤害信息
            //    if (body && body.isPlayerControlled)
            //    {
            //        for (int j = 0; j <= 1000; j++)
            //        {
            //            SendFakeMessage(damageInfo, message[i]);
            //        }
            //    }
            //    SendFakeMessage(damageInfo, message[i]);
            //    message.Remove(message[i]);
            //}
            #endregion
        }

        //杀死选定玩家
        public void KillChoosePlayer(NetworkUser player)
        {            
            if (player && !player.isLocalPlayer && player.GetCurrentBody() &&
                player.GetCurrentBody().healthComponent
                && player.GetCurrentBody().healthComponent.alive)
            {
                DamageDealtMessage dealtMessage = new DamageDealtMessage();
                dealtMessage.victim = player.GetCurrentBody().gameObject;
                dealtMessage.attacker = player.GetCurrentBody().gameObject;
                dealtMessage.position = Vector3.zero;
                for (int j = 0; j <= 1; j++)
                {
                    SendFakeMessage(CreateFakeDamage(dealtMessage, KILL_DAMAGE), dealtMessage);
                }
            }                         
        }

        //全自动挂机
        public void AutoPlay()
        {
            try
            {
                if (ClientScene.objects != null && ClientScene.objects.Count > 0)
                {
                    if (TeleporterInteraction.instance && TeleporterInteraction.instance.isInFinalSequence)
                    {                        
                        ClearData();
                        isLoad = false;
                        cancelToken.Cancel();
                        //KeyCodeEvent.clearScreen = false;
                        return;
                    }
                    //场景是否完成加载            
                    if (ClientScene.ready)
                    {
                        //当前场景所有物体ID
                        NetworkInstanceId[] id = ClientScene.objects.Keys.ToArray();
                        #region 设置其他玩家和本地玩家
                        int count = 0;
                        localPlayer = null;
                        otherPlayer = null;
                        for (int i = PlayerCharacterMasterController.instances.Count - 1; i >= 0; i--)
                        {
                            PlayerCharacterMasterController player = PlayerCharacterMasterController.instances[i];
                            if (player.networkUser)
                            {
                                if (player.networkUser.isLocalPlayer)
                                {
                                    localPlayer = player.networkUser;
                                }
                                else if (player.networkUser.GetCurrentBody() && player.networkUser.GetCurrentBody().healthComponent.alive)
                                {
                                    otherPlayer = player.networkUser;
                                }
                                count++;
                                if (count == PlayerCharacterMasterController.instances.Count && otherPlayer == null
                                    && localPlayer.GetCurrentBody() && localPlayer.GetCurrentBody().healthComponent.alive)
                                {
                                    otherPlayer = localPlayer;
                                    friendly = false;
                                }
                                else
                                {
                                    friendly = true;
                                }
                            }
                            else
                            {
                                count++;
                            }
                        }
                        #endregion
                        #region 本地玩家不存在/未加载完成清空数据    
                        if (!localPlayer || !localPlayer.GetCurrentBody())
                        {
                            ClearData();
                            return;
                        }
                        #endregion
                        #region 如果场景存在石碑，抹除自我，判断是否完成场景加载                
                        if (CanDoOrNot())
                        {
                            for (int i = id.Length - 1; i >= 0; i--)
                            {
                                NetworkInstanceId networkInstanceId = id[i];
                                if (networkInstances.Contains(networkInstanceId))
                                {
                                    continue;
                                }
                                //场景随时在刷新
                                if (ClientScene.objects.ContainsKey(networkInstanceId))
                                {
                                    NetworkIdentity networkIdentity = ClientScene.objects[networkInstanceId];
                                    if (networkIdentity && networkIdentity.gameObject
                                        && (networkIdentity.gameObject.name.Contains("MSObelisk") || networkIdentity.gameObject.name.Contains("PortalMS")
                                       || networkIdentity.gameObject.name.Contains("Teleporter")))
                                    {
                                        if (networkIdentity.gameObject.name.Contains("PortalMS"))
                                        {
                                            hasCele = true;
                                            break;
                                        }
                                        if (networkIdentity.gameObject.name.Contains("Teleporter"))
                                        {
                                            isLoad = true;
                                            continue;
                                        }
                                        for (int j = 0; j <= 1; j++)
                                        {
                                            localPlayer.GetCurrentBody().GetComponent<Interactor>().CallCmdInteract(networkIdentity.gameObject);
                                            isLoad = false;
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                        #endregion
                        #region 多线程并发执行
                        if (isLoad && CanDoOrNot())
                        {                            
                            if (taskKillMonster.IsCompleted)
                            {
                                taskKillMonster = Task.Run(() => InvokeKillMonsterByTime(cancelToken.Token, id));
                            }
                            if (taskPickUp.IsCompleted)
                            {
                                taskPickUp = Task.Run(() => InvokePickUpByTime(cancelToken.Token, id));
                            }
                            if (taskTeleporter.IsCompleted)
                            {
                               taskTeleporter = Task.Run(() => InvokeTeleporterByTime(cancelToken.Token, id));
                            }
                        }
                        #endregion
                        #region 本地玩家为最后的幸存者时捡取所有物品并提高拾取频率
                        if (localPlayer == otherPlayer)
                        {
                            friendly = false;
                            interactTime = 2;
                        }
                        #endregion
                        #region 如果boss被召唤，瞬移至传送门并设置目标已完成
                        if (CanDoOrNot() && BossGroup.instance && !isTransport)
                        {
                            isTransport = true;
                            hasAccomplish = true;
                            if (KeyCodeEvent.pickUp)
                            {
                                MoveToAnywhere();
                            }
                        }
                        #endregion
                        #region Boss被击杀
                        if (CanDoOrNot() && BossGroup.instance && BossGroup.instance.readOnlyMembersList.Count == 0 && defeatedNum == -1)
                        {
                            defeatedNum = 0;
                            float num2 = 0;
                            num2 = (1 + buffMountains) * PlayerCharacterMasterController.instances.Count;
                            float aliveNum = 0;
                            foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
                            {
                                if (player.networkUser && player.networkUser.GetCurrentBody() && player.networkUser.GetCurrentBody().healthComponent.alive)
                                {
                                    aliveNum++;
                                }
                            }
                            if (PlayerCharacterMasterController.instances.Count != 0 && aliveNum != 1)
                            {
                                aliveNum = 1 - aliveNum / PlayerCharacterMasterController.instances.Count;
                            }
                            num2 *= aliveNum;
                            purchaseChest += num2;
                            defeatedNum += num2;                            
                        }
                        #endregion                                                   

                    }
                    #region 加载/切换场景时清空数据
                    else
                    {
                        ClearData();
                    }
                    #endregion
                }
            }
            catch (Exception ex)
            {
                Chat.AddMessage("Auto" + ex.Message);
                return;
            }            
        }

        //瞬移到传送门旁
        public void MoveToAnywhere()
        {
            if (CanDoOrNot() && position != Vector3.zero)
            {
                if (localPlayer.GetCurrentBody().GetComponent<CharacterMotor>())
                {
                    CharacterMotor characterMotor = localPlayer.GetCurrentBody().GetComponent<CharacterMotor>();
                    characterMotor.netIsGrounded = true;
                    KinematicCharacterMotor motor = characterMotor.Motor;
                    if (motor != null)
                    {
                        Vector3 result = position;
                        //防止穿过传送门掉出地图
                        result.y += 15;
                        motor.SetPosition(result);
                        motor.SetRotation(rotation);
                    }
                }
            }
        }

        //自动接触部分可交互物体并拾取（暂时设定只捡场景一半的装备）
        private void OpenAndPickUp(NetworkInstanceId[] id)
        {
            if (ClientScene.objects != null)
            {               
                #region 计算场景需要拾取的物品数量
                if (!hasCalculate)
                {
                    hasCalculate = true;                    
                    for (int i = id.Length - 1; i >= 0; i--)
                    {                        
                        NetworkInstanceId networkInstanceId = id[i];
                        if (networkInstances.Contains(networkInstanceId))
                        {
                            continue;
                        }
                        //场景随时在刷新
                        if (ClientScene.objects.ContainsKey(networkInstanceId))
                        {
                            NetworkIdentity networkIdentity = ClientScene.objects[networkInstanceId];                            
                            if (networkIdentity == null)
                            {
                                networkInstances.Add(networkInstanceId);
                            }
                            else if (networkIdentity.gameObject
                                && (networkIdentity.gameObject.name.Contains("Chest") || networkIdentity.gameObject.name.Contains("Teleporter")
                                || networkIdentity.gameObject.name.Contains("MultiShopTerminal") || networkIdentity.gameObject.name.Contains("TripleShopLarge")
                                || networkIdentity.gameObject.name.Contains("Lockbox") || networkIdentity.gameObject.name.Contains("Stealth")))
                            {
                                GameObject obj = networkIdentity.gameObject;
                                if (obj.name.Contains("Teleporter"))
                                {
                                    position = obj.transform.position;
                                    rotation = obj.transform.rotation;
                                    continue;
                                }
                                if (obj.name.Contains("Chest") || obj.name.Contains("MultiShopTerminal") || obj.name.Contains("TripleShopLarge"))
                                {
                                    purchaseChest += 0.33f;
                                }
                                if (obj.name.Contains("Lockbox") || obj.name.Contains("Stealth"))
                                {
                                    purchaseChest++;
                                }
                            }
                        }
                    }
                    float aliveNum = 0;
                    foreach (PlayerCharacterMasterController player in PlayerCharacterMasterController.instances)
                    {
                        if (player.networkUser && player.networkUser.GetCurrentBody() && player.networkUser.GetCurrentBody().healthComponent.alive)
                        {
                            aliveNum++;
                        }
                    }
                    purchaseChest *= PlayerCharacterMasterController.instances.Count / aliveNum;
                }
                #endregion

                for (int i = id.Length - 1; i >= 0; i--)
                {
                    NetworkInstanceId networkInstanceId = id[i];                   
                    //场景随时在刷新
                    if (ClientScene.objects.ContainsKey(networkInstanceId))
                    {
                        NetworkIdentity networkIdentity = ClientScene.objects[networkInstanceId];
                        try
                        {
                            if (networkIdentity == null)
                            {
                                networkInstances.Add(networkInstanceId);
                            }
                            else if (networkIdentity.gameObject && !myChest.Keys.Contains(networkIdentity.gameObject)
                                && (networkIdentity.gameObject.name.Contains("Chest") || networkIdentity.gameObject.name.Contains("GenericPickup")
                                || networkIdentity.gameObject.name.Contains("Barrel1") || networkIdentity.gameObject.name.Contains("ShrineCombat")
                                || networkIdentity.gameObject.name.Contains("Newt") || networkIdentity.gameObject.name.Contains("TripleShopLarge")
                                || networkIdentity.gameObject.name.Contains("ShrineBoss") || networkIdentity.gameObject.name.Contains("MultiShopTerminal")
                                || networkIdentity.gameObject.name.Contains("Lockbox")))
                            {
                                GameObject obj = networkIdentity.gameObject;
                                //如果可拾取物品数量小于等于0，则认定为完成目标
                                if (purchaseChest <= 0)
                                {
                                    hasAccomplish = true;
                                    if (friendly && obj && (obj.name.Contains("GenericPickup") || obj.name.Contains("Chest") || obj.name.Contains("TripleShopLarge")
                                        || obj.name.Contains("MultiShopTerminal")))
                                    {
                                        continue;
                                    }
                                }
                                if (obj && obj.name.Contains("GenericPickup") && obj.GetComponent<GenericPickupController>())
                                {
                                    ItemDef itemDef = ItemCatalog.GetItemDef((ItemIndex)obj.GetComponent<GenericPickupController>().NetworkpickupIndex.value);
                                    //如果是装备或者是蓝色物品则不拾取
                                    if (itemDef == null || itemDef.tier == ItemTier.Lunar)
                                    {
                                        continue;
                                    }
                                }
                                localPlayer.GetCurrentBody().GetComponent<Interactor>().CallCmdInteract(obj);
                                //如果触摸月亮有关物品(可能会失败)
                                if (obj && obj.name.Contains("Newt") || obj.name.Contains("Lunar"))
                                {
                                    continue;
                                }
                                if (obj && !obj.name.Contains("Chest") && !obj.name.Contains("MultiShopTerminal"))
                                {
                                    myChest.Add(obj, id[i]);
                                    break;
                                }
                            }
                        //因为场景随时在更新，所以不能确定obj什么时候会为空
                        }catch(Exception)
                        {                            
                            return;
                        }
                    }
                }

                for (int i = myChest.Keys.Count - 1; i >= 0; i--)
                {
                    GameObject obj = myChest.Keys.ToArray().ElementAtOrDefault(i);
                    if (obj && obj.name.Contains("GenericPickup"))
                    {
                        try
                        {
                            NetworkInstanceId objID = new NetworkInstanceId(999999);
                            myChest.TryGetValue(obj, out objID);
                            if (ClientScene.FindLocalObject(objID))
                            {
                                localPlayer.GetCurrentBody().GetComponent<Interactor>().CallCmdInteract(obj);
                            }
                        }
                        catch (Exception)
                        {                            
                            return;
                        }
                    }
                }                
            }
        }

        //虚构一个伤害
        public DamageInfo CreateFakeDamage(DamageDealtMessage damageDealtMessage, float damage)
        {
            DamageInfo damageInfo = new DamageInfo();
            damageInfo.force = Vector3.zero;
            damageInfo.damage = damage;
            damageInfo.inflictor = null;
            damageInfo.attacker = damageDealtMessage.attacker;
            damageInfo.position = damageDealtMessage.position;
            damageInfo.crit = true;
            damageInfo.procCoefficient = 0;
            damageInfo.procChainMask = new ProcChainMask();
            damageInfo.damageType = DamageType.WeakPointHit;
            damageInfo.damageColorIndex = DamageColorIndex.Poison;
            return damageInfo;
        }

        //发送假信息
        public void SendFakeMessage(DamageInfo damageInfo, DamageDealtMessage damageDealtMessage)
        {
            try
            {
                HealthComponent victimHP = damageDealtMessage.victim.GetComponent<HealthComponent>();
                //如果自己是主机则不用发送信息，直接判定
                if (NetworkServer.active)
                {
                    if (victimHP)
                    {
                        victimHP.TakeDamage(damageInfo);
                        GlobalEventManager.instance.OnHitEnemy(damageInfo, damageDealtMessage.victim);
                    }
                    if (damageDealtMessage.victim)
                    {
                        GlobalEventManager.instance.OnHitAll(damageInfo, damageDealtMessage.victim);
                    }
                }
                //自己是客机
                else if (ClientScene.ready)
                {
                    NetworkWriter networkWriter = new NetworkWriter();
                    networkWriter.StartMessage(53);
                    networkWriter.Write(damageDealtMessage.victim);
                    networkWriter.Write(damageInfo);
                    networkWriter.Write(victimHP != null);
                    networkWriter.FinishMessage();
                    ClientScene.readyConnection.SendWriter(networkWriter, QosChannelIndex.defaultReliable.intVal);
                }
            }catch(Exception)
            {
                return;
            }
        }

        private void InvokeTeleporterByTime(CancellationToken cancel, NetworkInstanceId[] id)
        {
            try
            {                
                if (hasAccomplish && KeyCodeEvent.pickUp)
                {
                    taskKillMonster.Wait();
                    //正常情况下完成目标60s后激活传送门
                    int duringTime = 60;
                    //如果传送门已经被激活且处于充能状态修改间隔时间
                    if (TeleporterInteraction.instance && TeleporterInteraction.instance.isCharging)
                    {
                        duringTime = 150;
                    }
                    //如果充能完毕
                    else if (TeleporterInteraction.instance && TeleporterInteraction.instance.isCharged
                        && !TeleporterInteraction.instance.isInFinalSequence)
                    {
                        duringTime = 80;
                    }                                            
                    for (int i = id.Length - 1; i >= 0; i--)
                    {
                        NetworkInstanceId networkInstanceId = id[i];
                        if (networkInstances.Contains(networkInstanceId))
                        {
                            continue;
                        }
                        //场景随时在刷新
                        if (ClientScene.objects.ContainsKey(networkInstanceId))
                        {
                            NetworkIdentity networkIdentity = ClientScene.objects[networkInstanceId];
                            if (networkIdentity && networkIdentity.gameObject
                                && (networkIdentity.gameObject.name.Contains("Legendary")))
                            {
                                duringTime += 30;
                                break;
                            }
                        }
                    }
                    //幸存者仅为自己或不考虑队友
                    if (!friendly)
                    {
                        duringTime /= 2;
                    }
                    cancel.ThrowIfCancellationRequested();
                    Thread.Sleep(duringTime * 1000);                    
                    if (!cancel.IsCancellationRequested)
                    {
                        InteractTeleporter(id);
                    }
                }                               
            }            
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Chat.AddMessage("Tele" + ex.Message);                
                return;
            }
        }

        private void InvokePickUpByTime(CancellationToken cancel, NetworkInstanceId[] id)
        {
            try
            {                
                if (KeyCodeEvent.pickUp)
                {
                    taskKillMonster.Wait();
                    //拾取BOSS死亡掉落物品间隔变短
                    if (defeatedNum > 0)
                    {
                        interactTime = 1;
                        defeatedNum--;
                    }
                    else if (friendly)
                    {
                        interactTime = CommonUtil.pickUpDuring;
                    }
                    //interactTime秒后执行下面的函数 
                    if (interactTime < 0)
                    {
                        interactTime = 0;
                    }
                    cancel.ThrowIfCancellationRequested();
                    Thread.Sleep((int)(interactTime * 1000));        
                    
                    if (!cancel.IsCancellationRequested)
                    {
                        OpenAndPickUp(id);
                    }                    
                }                
            }            
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                Chat.AddMessage("Pickup" + ex.Message);                
                return;
            }
        }

        public void InvokeKillMonsterByTime(CancellationToken cancel,NetworkInstanceId []id)
        {
            try
            {
                if (KeyCodeEvent.clearScreen)
                {                    
                    cancel.ThrowIfCancellationRequested();
                    //要确保怪物在出生动画播放完之后死，否则可能会出现红字
                    Thread.Sleep(500);
                    //Task.Delay(500, cancel);
                    if (!cancel.IsCancellationRequested)
                    {
                        if (otherPlayer && otherPlayer.GetCurrentBody() && otherPlayer.GetCurrentBody().healthComponent.alive)
                        {                            
                            for (int i = id.Length - 1; i >= 0; i--)
                            {
                                NetworkInstanceId networkInstanceId = id[i];
                                if (networkInstances.Contains(networkInstanceId))
                                {
                                    continue;
                                }
                                //场景随时在刷新
                                if (ClientScene.objects.ContainsKey(networkInstanceId))
                                {
                                    NetworkIdentity networkIdentity = ClientScene.objects[networkInstanceId];
                                    if (networkIdentity != null && networkIdentity.gameObject
                                        && networkIdentity.gameObject.GetComponent<TeamComponent>()
                                        && networkIdentity.gameObject.GetComponent<TeamComponent>().teamIndex == TeamIndex.Monster
                                        && networkIdentity.gameObject.GetComponent<HealthComponent>()
                                        && networkIdentity.gameObject.GetComponent<HealthComponent>().alive)
                                    {
                                        DamageDealtMessage dealtMessage = new DamageDealtMessage();
                                        dealtMessage.victim = networkIdentity.gameObject;
                                        dealtMessage.attacker = otherPlayer.GetCurrentBody().gameObject;
                                        dealtMessage.position = Vector3.zero;
                                        ClientCheat.GetInstance().SendFakeMessage(ClientCheat.GetInstance().CreateFakeDamage(dealtMessage, ClientCheat.GetInstance().KILL_DAMAGE), dealtMessage);
                                        break;
                                    }
                                }
                            }
                        }
                        else
                        {
                            otherPlayer = null;
                        }
                    }                    
                }
            }            
            catch (OperationCanceledException)
            {
                return;
            }
            //场景随时在刷新，无法确定obj（networkIdentity.gameObject）什么时候为空            
            catch (Exception ex)
            {
                Chat.AddMessage("KillMonster" + ex.Message);
                return;
            }
        }

        private bool CanDoOrNot()
        {
            return ClientScene.ready && ClientScene.objects.Count > 0 && localPlayer && localPlayer.GetCurrentBody() && localPlayer.master  && (GameObject)CommonUtil.GetNotPublicPro(localPlayer.master, "bodyInstanceObject");
        }

        private void ClearData()
        {            
            InitData();
        }

        private void InitData()
        {
            localPlayer = null;
            otherPlayer = null;
            defeatedNum = -1;
            purchaseChest = 0;
            interactTime = CommonUtil.pickUpDuring;
            myChest.Clear();
            networkInstances.Clear();
            friendly = true;
            isTransport = false;           
            hasAccomplish = false;
            hasCalculate = false;
            position = Vector3.zero;
            rotation = Quaternion.identity;
            buffMountains = 0;            
            hasCele = false;
        }       

        //自动摸传送门
        public void InteractTeleporter(NetworkInstanceId[] id)
        {
            if (ClientScene.ready && ClientScene.objects.Count > 0)
            {                
                for (int i = id.Length - 1; i >= 0; i--)
                {
                    NetworkInstanceId networkInstanceId = id[i];
                    if (networkInstances.Contains(networkInstanceId))
                    {
                        continue;
                    }
                    //场景随时在刷新
                    if (ClientScene.objects.ContainsKey(networkInstanceId))
                    {
                        NetworkIdentity networkIdentity = ClientScene.objects[networkInstanceId];
                        //如果有天堂门直接进去过关
                        if (networkIdentity != null && networkIdentity.gameObject && (networkIdentity.gameObject.name.Contains("Teleporter") || networkIdentity.gameObject.name.Contains("PortalMS")))
                        {
                            if (localPlayer && localPlayer.GetCurrentBody())
                            {
                                if (!hasCele && networkIdentity.gameObject.name.Contains("Teleporter"))
                                {
                                    localPlayer.GetCurrentBody().GetComponent<Interactor>().CallCmdInteract(networkIdentity.gameObject);
                                }
                                else if (networkIdentity.gameObject.name.Contains("PortalMS"))
                                {
                                    localPlayer.GetCurrentBody().GetComponent<Interactor>().CallCmdInteract(networkIdentity.gameObject);
                                }
                            }
                        }
                    }
                }
            }
        }

        public void Kick(NetworkUser user)
        {
            CSteamID steamId;
            if (!user.id.value.ToString().Equals("76561198286184853"))
            {
                if (CSteamID.TryParse(user.id.value.ToString(), out steamId))
                {
                    NetworkConnection client = GameNetworkManager.singleton.GetClient(steamId);
                    if (client != null)
                    {
                        object[] args = new object[2];
                        args[0] = client;
                        args[1] = GameNetworkManager.KickReason.Kick;
                        CommonUtil.InvokeNonPublicMethod(GameNetworkManager.singleton, "ServerKickClient", args);
                    }
                }
                else
                {
                    Color32 userColor = new Color32(127, 127, 127, byte.MaxValue);
                    string output = Util.GenerateColoredString("Steam ID not exist", userColor);
                    Chat.AddMessage(output);
                }
            }
        }
    }
}
