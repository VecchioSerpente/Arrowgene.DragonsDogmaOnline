using System;
using System.Collections.Generic;
using System.Linq;
using Arrowgene.Ddon.Database;
using Arrowgene.Ddon.GameServer.Handler;
using Arrowgene.Ddon.Server;
using Arrowgene.Ddon.Shared.Entity;
using Arrowgene.Ddon.Shared.Entity.PacketStructure;
using Arrowgene.Ddon.Shared.Entity.Structure;
using Arrowgene.Ddon.Shared.Model;

namespace Arrowgene.Ddon.GameServer.Characters
{
    public class JobManager
    {
        public void SetJob(DdonServer<GameClient> server, GameClient client, CharacterCommon common, JobId jobId)
        {
            common.Job = jobId;

            server.Database.UpdateCharacterCommonBaseInfo(common);

            CDataCharacterJobData? activeCharacterJobData = common.ActiveCharacterJobData;

            if(activeCharacterJobData == null)
            {
                activeCharacterJobData = new CDataCharacterJobData();
                activeCharacterJobData.Job = jobId;
                activeCharacterJobData.Exp = 0;
                activeCharacterJobData.JobPoint = 0;
                activeCharacterJobData.Lv = 1;
                // TODO: All the other stats
                common.CharacterJobDataList.Add(activeCharacterJobData);
                server.Database.ReplaceCharacterJobData(common.CommonId, activeCharacterJobData);
            }

            // TODO: Figure out if it should send all equips or just the ones for the current job
            List<CDataEquipItemInfo> equipItemInfos = common.Equipment.getEquipmentAsCDataEquipItemInfo(common.Job, EquipType.Performance)
                .Union(common.Equipment.getEquipmentAsCDataEquipItemInfo(common.Job, EquipType.Visual))
                .ToList();
            List<CDataCharacterEquipInfo> characterEquipList = common.Equipment.getEquipmentAsCDataCharacterEquipInfo(common.Job, EquipType.Performance)
                .Union(common.Equipment.getEquipmentAsCDataCharacterEquipInfo(common.Job, EquipType.Visual))
                .ToList();

            List<CDataSetAcquirementParam> skills = common.EquippedCustomSkillsDictionary[jobId]
                .Select((x, idx) => x.AsCDataSetAcquirementParam((byte)(idx+1)))
                .ToList();
            List<CDataSetAcquirementParam> abilities = common.EquippedAbilitiesDictionary[jobId]
                .Select((x, idx) => x.AsCDataSetAcquirementParam((byte)(idx+1)))
                .ToList();
            List<CDataLearnNormalSkillParam> normalSkills = common.LearnedNormalSkills
                .Select(x => new CDataLearnNormalSkillParam(x))
                .ToList();
            List<CDataEquipJobItem> jobItems = common.CharacterEquipJobItemListDictionary[common.Job];

            S2CItemUpdateCharacterItemNtc updateCharacterItemNtc = new S2CItemUpdateCharacterItemNtc();
            // TODO: Move previous job equipment to storage box, and move new job equipment from storage box

            if(common is Character)
            {
                Character character = (Character) common;

                S2CJobChangeJobNtc changeJobNotice = new S2CJobChangeJobNtc();
                changeJobNotice.CharacterId = character.CharacterId;
                changeJobNotice.CharacterJobData = activeCharacterJobData;
                changeJobNotice.EquipItemInfo = equipItemInfos;
                changeJobNotice.SetAcquirementParamList = skills;
                changeJobNotice.SetAbilityParamList = abilities;
                changeJobNotice.LearnNormalSkillParamList = normalSkills;
                changeJobNotice.EquipJobItemList = jobItems;
                // TODO: Unk0
                
                foreach(GameClient otherClient in server.ClientLookup.GetAll())
                {
                    otherClient.Send(changeJobNotice);
                }

                updateCharacterItemNtc.UpdateType = 0x28;
                client.Send(updateCharacterItemNtc);

                S2CJobChangeJobRes changeJobResponse = new S2CJobChangeJobRes();
                changeJobResponse.CharacterJobData = activeCharacterJobData;
                changeJobResponse.CharacterEquipList = characterEquipList;
                changeJobResponse.SetAcquirementParamList = skills;
                changeJobResponse.SetAbilityParamList = abilities;
                changeJobResponse.LearnNormalSkillParamList = normalSkills;
                changeJobResponse.EquipJobItemList = jobItems;
                changeJobResponse.PlayPointData = character.PlayPointList
                    .Where(x => x.Job == jobId)
                    .Select(x => x.PlayPoint)
                    .FirstOrDefault(new CDataPlayPointData());
                changeJobResponse.Unk0.Unk0 = (byte) jobId;
                changeJobResponse.Unk0.Unk1 = character.Storage.getAllStoragesAsCDataCharacterItemSlotInfoList();
            
                client.Send(changeJobResponse);
            }
            else if(common is Pawn)
            {
                Pawn pawn = (Pawn) common;

                S2CJobChangePawnJobNtc changeJobNotice = new S2CJobChangePawnJobNtc();
                changeJobNotice.CharacterId = pawn.CharacterId;
                changeJobNotice.PawnId = pawn.PawnId;
                changeJobNotice.CharacterJobData = activeCharacterJobData;
                changeJobNotice.EquipItemInfo = equipItemInfos;
                changeJobNotice.SetAcquirementParamList = skills;
                changeJobNotice.SetAbilityParamList = abilities;
                changeJobNotice.LearnNormalSkillParamList = normalSkills;
                changeJobNotice.EquipJobItemList = jobItems;
                // TODO: Unk0
                foreach(GameClient otherClient in server.ClientLookup.GetAll())
                {
                    otherClient.Send(changeJobNotice);
                }

                updateCharacterItemNtc.UpdateType = 0x29;
                client.Send(updateCharacterItemNtc);

                S2CJobChangePawnJobRes changeJobResponse = new S2CJobChangePawnJobRes();
                changeJobResponse.PawnId = pawn.PawnId;
                changeJobResponse.CharacterJobData = activeCharacterJobData;
                changeJobResponse.CharacterEquipList = characterEquipList;
                changeJobResponse.SetAcquirementParamList = skills;
                changeJobResponse.SetAbilityParamList = abilities;
                changeJobResponse.LearnNormalSkillParamList = normalSkills;
                changeJobResponse.EquipJobItemList = jobItems;
                changeJobResponse.Unk0.Unk0 = (byte) jobId;
                // changeJobResponse.Unk0.Unk1 = pawn.Storage.getAllStoragesAsCDataCharacterItemSlotInfoList(); // TODO: What
                // changeJobResponse.Unk1 // TODO: its the same thing as in CDataPawnInfo
                changeJobResponse.SpSkillList = pawn.SpSkillList;
                client.Send(changeJobResponse);
            }
            else
            {
                throw new Exception("Unknown character type");
            }
        }

        public void UnlockSkill(IDatabase database, GameClient client, CharacterCommon character, JobId job, uint skillId, byte skillLv)
        {
            CustomSkill newSkill = new CustomSkill()
            {
                Job = job,
                SkillId = skillId,
                SkillLv = skillLv
            };
            character.LearnedCustomSkills.Add(newSkill);
            database.ReplaceLearnedCustomSkill(character.CommonId, newSkill);

            uint jpCost = SkillGetAcquirableSkillListHandler.AllSkills
                .Where(skill => skill.Job == job && skill.SkillNo == skillId)
                .SelectMany(skill => skill.Params)
                .Where(skillParams => skillParams.Lv == skillLv)
                .Select(skillParams => skillParams.RequireJobPoint)
                .Single();

            // TODO: Check that this doesn't end up negative
            CDataCharacterJobData activeCharacterJobData = character.ActiveCharacterJobData;
            activeCharacterJobData.JobPoint -= jpCost;
            database.UpdateCharacterJobData(character.CommonId, activeCharacterJobData);

            if(character is Character)
            {
                client.Send(new S2CSkillLearnSkillRes()
                {
                    Job = job,
                    NewJobPoint = activeCharacterJobData.JobPoint,
                    SkillId = skillId,
                    SkillLv = skillLv
                });
            }
            else if(character is Pawn)
            {
                client.Send(new S2CSkillLearnPawnSkillRes()
                {
                    PawnId = ((Pawn) character).PawnId,
                    Job = job,
                    NewJobPoint = activeCharacterJobData.JobPoint,
                    SkillId = skillId,
                    SkillLv = skillLv
                });
            }
        }

        public CustomSkill SetSkill(IDatabase database, GameClient client, CharacterCommon character, JobId job, byte slotNo, uint skillId, byte skillLv)
        {
            // TODO: Check in DB if the skill is unlocked and it's leveled up to what the packet says
            CustomSkill skill = character.LearnedCustomSkills.Where(skill => skill.Job == job && skill.SkillId == skillId).Single();
            character.EquippedCustomSkillsDictionary[job][slotNo-1] = skill;

            database.ReplaceEquippedCustomSkill(character.CommonId, slotNo, skill);

            // Inform party members of the change
            if(job == character.Job)
            {
                if(character is Character)
                {
                    client.Party.SendToAll(new S2CSkillCustomSkillSetNtc()
                    {
                        CharacterId = ((Character) character).CharacterId,
                        ContextAcquirementData = skill.AsCDataContextAcquirementData(slotNo)
                    });
                }
                else if(character is Pawn)
                {
                    client.Party.SendToAll(new S2CSkillPawnCustomSkillSetNtc()
                    {
                        PawnId = ((Pawn) character).PawnId,
                        ContextAcquirementData = skill.AsCDataContextAcquirementData(slotNo)
                    });
                }
            }

            return skill;
        }

        public IEnumerable<byte> ChangeExSkill(IDatabase database, GameClient client, CharacterCommon character, JobId job, uint skillId)
        {
            CustomSkill affectedSkill = character.LearnedCustomSkills
                .Where(skill => skill.Job == job && skill.SkillId == skillId)
                .Single();
            
            List<byte> affectedSlots = new List<byte>(); 
            foreach(KeyValuePair<JobId, List<CustomSkill>> jobAndEquippedSkill in character.EquippedCustomSkillsDictionary)
            {
                for(int i=0; i<jobAndEquippedSkill.Value.Count; i++)
                {
                    CustomSkill equippedSkill = jobAndEquippedSkill.Value[i];
                    byte slotNo = (byte)(i+1);
                    if(equippedSkill == affectedSkill)
                    {
                        SetSkill(database, client, character, affectedSkill.Job, slotNo, affectedSkill.SkillId, affectedSkill.SkillLv);
                        affectedSlots.Add(slotNo);
                        break;
                    }
                }
            }
            return affectedSlots;
        }

        public void RemoveSkill(IDatabase database, CharacterCommon character, JobId job, byte slotNo)
        {
            character.EquippedCustomSkillsDictionary[job][slotNo-1] = null;

            // TODO: Error handling
            database.DeleteEquippedCustomSkill(character.CommonId, job, slotNo);

            // I haven't found a packet to notify this to other players
            // From what I tested it doesn't seem to be necessary
        }

        public void UnlockAbility(IDatabase database, GameClient client, CharacterCommon character, JobId job, uint abilityId, byte abilityLv)
        {
            Ability newAbility = new Ability()
            {
                Job = job,
                AbilityId = abilityId,
                AbilityLv = abilityLv
            };
            character.LearnedAbilities.Add(newAbility);
            database.ReplaceLearnedAbility(character.CommonId, newAbility);

            uint jpCost = SkillGetAcquirableAbilityListHandler.AllAbilities
                .Where(aug => aug.Job == job && aug.AbilityNo == abilityId)
                .SelectMany(aug => aug.Params)
                .Where(augParams => augParams.Lv == abilityLv)
                .Select(augParams => augParams.RequireJobPoint)
                .Single();

            // TODO: Check that this doesn't end up negative
            CDataCharacterJobData activeCharacterJobData = character.ActiveCharacterJobData;
            activeCharacterJobData.JobPoint -= jpCost;
            database.UpdateCharacterJobData(character.CommonId, activeCharacterJobData);

            if(character is Character)
            {
                client.Send(new S2CSkillLearnAbilityRes()
                {
                    Job = job,
                    NewJobPoint = activeCharacterJobData.JobPoint,
                    AbilityId = abilityId,
                    AbilityLv = abilityLv
                });
            }
            else if(character is Pawn)
            {
                // TODO: S2CSkillLearnPawnAbilityRes
            }
        }

        public Ability SetAbility(IDatabase database, GameClient client, CharacterCommon character, JobId abilityJob, byte slotNo, uint abilityId, byte abilityLv)
        {
            Ability ability = character.LearnedAbilities
                .Where(aug => aug.Job == abilityJob && aug.AbilityId == abilityId && aug.AbilityLv == abilityLv)
                .Single();

            character.EquippedAbilitiesDictionary[character.Job][slotNo-1] = ability;

            database.ReplaceEquippedAbility(character.CommonId, character.Job, slotNo, ability);

            // Inform party members of the change
            if(character is Character)
            {
                client.Party.SendToAll(new S2CSkillAbilitySetNtc()
                {
                    CharacterId = ((Character) character).CharacterId,
                    ContextAcquirementData = ability.AsCDataContextAcquirementData(slotNo)
                });
            }
            else if(character is Pawn)
            {
                client.Party.SendToAll(new S2CSkillPawnAbilitySetNtc()
                {
                    PawnId = ((Pawn) character).PawnId,
                    ContextAcquirementData = ability.AsCDataContextAcquirementData(slotNo)
                });
            }

            return ability;
        }

        public void RemoveAbility(IDatabase database, CharacterCommon character, byte slotNo)
        {
            // TODO: Performance
            List<Ability> equippedAbilities = character.EquippedAbilitiesDictionary[character.Job];
            lock(equippedAbilities)
            {
                byte removedAbilitySlotNo = Byte.MaxValue;
                for(int i=0; i<equippedAbilities.Count; i++)
                {
                    Ability equippedAbility = equippedAbilities[i];
                    byte equippedAbilitySlotNo = (byte)(i+1);
                    if(character.Job == character.Job && equippedAbilitySlotNo == slotNo)
                    {
                        equippedAbilities.RemoveAt(i);
                        removedAbilitySlotNo = equippedAbilitySlotNo;
                        break;
                    }
                }

                for(int i=0; i<equippedAbilities.Count; i++)
                {
                    Ability equippedAbility = equippedAbilities[i];
                    byte equippedAbilitySlotNo = (byte)(i+1);
                    if(character.Job == character.Job)
                    {
                        if(equippedAbilitySlotNo > removedAbilitySlotNo)
                        {
                            equippedAbilitySlotNo--;
                        }
                    }
                }
            }

            database.ReplaceEquippedAbilities(character.CommonId, character.Job, equippedAbilities);

            // Same as skills, i haven't found an Ability off NTC. It may not be required
        }
    }
}