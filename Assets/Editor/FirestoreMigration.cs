using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;
using Auth;
using DB;
using Game;

namespace EditorTools
{
    /// <summary>
    /// Seeds the Battledawn Firestore database with the base configuration required by the editor tooling.
    /// </summary>
    public static class FirestoreMigration
    {
        private const string TargetDatabaseId = "battledawnpro";
        private const int SchemaVersion = 1;

        public static async Task RunAsync()
        {
            try
            {
                await EnsureAuthenticatedAsync();

                // Make sure Firestore is initialised against the expected database.
                DB.Default.Init(TargetDatabaseId);
                FirebaseFirestore firestore = DB.Default.Database;
                if (firestore == null)
                {
                    throw new InvalidOperationException("Firestore could not be initialised.");
                }

                await SeedGameConfigAsync(firestore);

                Debug.Log("[FirestoreMigration] Migration completed successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FirestoreMigration] Migration failed: {ex.Message}");
                throw;
            }
        }

        private static async Task EnsureAuthenticatedAsync()
        {
            if (User.user != null)
            {
                return;
            }

            var firebaseUser = await User.EnsureLoggedInAsync();
            if (firebaseUser == null)
            {
                throw new InvalidOperationException("A Firebase user is required to run the migration.");
            }
        }

        private static async Task SeedGameConfigAsync(FirebaseFirestore firestore)
        {
            DocumentReference attributeConfigDoc = firestore.Collection("gameConfig").Document("AttributeConfig");
            await attributeConfigDoc.SetAsync(new Dictionary<string, object>
            {
                { "schemaVersion", SchemaVersion },
                { "updatedAt", Timestamp.GetCurrentTimestamp() }
            }, SetOptions.MergeAll);

            IReadOnlyCollection<Game.Attribute> attributes = BuildAttributes();
            await SeedAttributesAsync(attributeConfigDoc.Collection("Attributes"), attributes);

            IReadOnlyCollection<Buff> buffs = BuildBuffs(attributes);
            await SeedBuffsAsync(attributeConfigDoc.Collection("Buffs"), buffs);

            IReadOnlyCollection<Research> researches = BuildResearches(buffs);
            await SeedResearchAsync(attributeConfigDoc.Collection("Researches"), researches);
        }

        private static IReadOnlyCollection<Game.Attribute> BuildAttributes()
        {
            return new[]
            {
                CreateAttribute("attack-damage", "Attack Damage", "attackDamage", Game.Attribute.AttributeTypeEnum.Buff, 12f),
                CreateAttribute("defense-matrix", "Defense Matrix", "defenseMatrix", Game.Attribute.AttributeTypeEnum.HomefieldAdvantage, 8f),
                CreateAttribute("mobilization", "Mobilization Speed", "mobilizationSpeed", Game.Attribute.AttributeTypeEnum.DurationInMinutes, 3f),
                CreateAttribute("production-yield", "Production Yield", "productionYield", Game.Attribute.AttributeTypeEnum.Buff, 10f)
            };
        }

        private static async Task SeedAttributesAsync(CollectionReference collection, IEnumerable<Game.Attribute> attributes)
        {
            foreach (Game.Attribute attribute in attributes)
            {
                string documentId = attribute.Id ?? DB.Default.GenerateId(15);
                attribute.Id = documentId;
                await collection.Document(documentId).SetAsync(attribute, SetOptions.MergeAll);
            }
        }

        private static IReadOnlyCollection<Buff> BuildBuffs(IEnumerable<Game.Attribute> attributes)
        {
            Game.Attribute attack = attributes.First(a => a.Id == "attack-damage");
            Game.Attribute defense = attributes.First(a => a.Id == "defense-matrix");
            Game.Attribute speed = attributes.First(a => a.Id == "mobilization");
            Game.Attribute production = attributes.First(a => a.Id == "production-yield");

            return new[]
            {
                CreateBuff(
                    id: "buff-orbital-salvo",
                    name: "Orbital Salvo",
                    description: "Increases offensive damage for the next engagement.",
                    spritePath: "UI/BuffIcons/AttackBuff",
                    researchMinutes: 30,
                    attributes: new[] { attack }),
                CreateBuff(
                    id: "buff-aegis-barrier",
                    name: "Aegis Barrier",
                    description: "Deploys a temporary shield that reduces incoming damage.",
                    spritePath: "UI/BuffIcons/Shield",
                    researchMinutes: 45,
                    attributes: new[] { defense }),
                CreateBuff(
                    id: "buff-logistics-network",
                    name: "Logistics Network",
                    description: "Enhances mobilization speed for deployment orders.",
                    spritePath: "UI/BuffIcons/SpeedBuff",
                    researchMinutes: 20,
                    attributes: new[] { speed }),
                CreateBuff(
                    id: "buff-production-directive",
                    name: "Production Directive",
                    description: "Boosts base production yields for a short duration.",
                    spritePath: "UI/BuffIcons/ManufacturingBuff",
                    researchMinutes: 60,
                    attributes: new[] { production })
            };
        }

        private static async Task SeedBuffsAsync(CollectionReference collection, IEnumerable<Buff> buffs)
        {
            foreach (Buff buff in buffs)
            {
                string documentId = buff.Id ?? DB.Default.GenerateId(15);
                buff.Id = documentId;
                await collection.Document(documentId).SetAsync(buff, SetOptions.MergeAll);
            }
        }

        private static IReadOnlyCollection<Research> BuildResearches(IEnumerable<Buff> buffs)
        {
            Buff[] buffArray = buffs.Select(CloneBuff).ToArray();
            if (buffArray.Length < 4)
            {
                throw new InvalidOperationException("At least four buffs are required to seed the research catalogue.");
            }

            return new[]
            {
                CreateResearch(
                    id: "research-foundation",
                    name: "Strategic Foundation",
                    description: "Unlocks the first tier of research directives.",
                    level: 0,
                    researchType: Research.ResearchTypeEnum.Research,
                    researchMinutes: 90,
                    buffArray),
                CreateResearch(
                    id: "base-logistics",
                    name: "Base Logistics",
                    description: "Introduces base support protocols.",
                    level: 0,
                    researchType: Research.ResearchTypeEnum.Base,
                    researchMinutes: 90,
                    buffArray),
                CreateResearch(
                    id: "military-briefing",
                    name: "Military Briefing",
                    description: "Establishes military readiness procedures.",
                    level: 0,
                    researchType: Research.ResearchTypeEnum.Military,
                    researchMinutes: 90,
                    buffArray),
                CreateResearch(
                    id: "commander-orientation",
                    name: "Commander Orientation",
                    description: "Trains commanders on tactical systems.",
                    level: 0,
                    researchType: Research.ResearchTypeEnum.Commander,
                    researchMinutes: 90,
                    buffArray)
            };
        }

        private static async Task SeedResearchAsync(CollectionReference collection, IEnumerable<Research> researches)
        {
            foreach (Research research in researches)
            {
                string documentId = research.Id ?? DB.Default.GenerateId(15);
                research.Id = documentId;
                EnsureResearchSlots(research);
                await collection.Document(documentId).SetAsync(research, SetOptions.MergeAll);
            }
        }

        private static Game.Attribute CreateAttribute(string id, string name, string key, Game.Attribute.AttributeTypeEnum type, float effectValue)
        {
            return new Game.Attribute
            {
                Id = id,
                AttributeName = name,
                AttributeKey = key,
                AttributeType = type,
                AttributeFloat = effectValue,
                EffectValue = effectValue
            };
        }

        private static Buff CreateBuff(string id, string name, string description, string spritePath, int researchMinutes, IEnumerable<Game.Attribute> attributes)
        {
            ResearchTime researchTime = new ResearchTime
            {
                days = 0,
                hours = researchMinutes / 60,
                minutes = researchMinutes % 60,
                seconds = 0
            };

            return new Buff
            {
                Id = id,
                BuffName = name,
                BuffDescription = description,
                SpritePath = spritePath,
                attributeList = attributes.Select(CloneAttribute).ToList(),
                ResearchTimeToLevelUp = researchTime,
                CurrentResearchTime = new CurrentResearchTime
                {
                    LevelUpStartTime = string.Empty,
                    LevelUpFinishedTime = string.Empty,
                    ResearchTimeToLevelUp = new ResearchTime
                    {
                        days = researchTime.days,
                        hours = researchTime.hours,
                        minutes = researchTime.minutes,
                        seconds = researchTime.seconds
                    }
                }
            };
        }

        private static Research CreateResearch(string id, string name, string description, int level, Research.ResearchTypeEnum researchType, int researchMinutes, IReadOnlyList<Buff> buffs)
        {
            ResearchTime researchTime = new ResearchTime
            {
                days = 0,
                hours = researchMinutes / 60,
                minutes = researchMinutes % 60,
                seconds = 0
            };

            Research research = new Research
            {
                Id = id,
                ResearchName = name,
                ResearchDescription = description,
                ResearchLevel = level,
                ResearchType = researchType,
                ResearchTimeToLevelUp = researchTime,
                BuffCompletedSpritePath = "UI/BuffIcons/ResearchIcon",
                ReadyToLevelUp = false,
                CurrentResearchTime = new CurrentResearchTime
                {
                    LevelUpStartTime = string.Empty,
                    LevelUpFinishedTime = string.Empty,
                    ResearchTimeToLevelUp = new ResearchTime
                    {
                        days = researchTime.days,
                        hours = researchTime.hours,
                        minutes = researchTime.minutes,
                        seconds = researchTime.seconds
                    }
                }
            };

            Buff[] buffer = new Buff[4];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = CloneBuff(buffs[i % buffs.Count]);
            }
            research.BuffList = buffer;

            return research;
        }

        private static void EnsureResearchSlots(Research research)
        {
            if (research.BuffList == null || research.BuffList.Length != 4)
            {
                Buff[] slots = new Buff[4];
                if (research.BuffList != null)
                {
                    for (int i = 0; i < Math.Min(research.BuffList.Length, slots.Length); i++)
                    {
                        slots[i] = research.BuffList[i];
                    }
                }
                research.BuffList = slots;
            }
        }

        private static Game.Attribute CloneAttribute(Game.Attribute source)
        {
            return new Game.Attribute
            {
                Id = source.Id,
                AttributeName = source.AttributeName,
                AttributeKey = source.AttributeKey,
                AttributeType = source.AttributeType,
                AttributeFloat = source.AttributeFloat,
                EffectValue = source.EffectValue
            };
        }

        private static Buff CloneBuff(Buff source)
        {
            return new Buff
            {
                Id = source.Id,
                BuffName = source.BuffName,
                BuffDescription = source.BuffDescription,
                SpritePath = source.SpritePath,
                attributeList = source.attributeList?.Select(CloneAttribute).ToList() ?? new List<Game.Attribute>(),
                ResearchTimeToLevelUp = CloneResearchTime(source.ResearchTimeToLevelUp),
                CurrentResearchTime = new CurrentResearchTime
                {
                    LevelUpStartTime = source.CurrentResearchTime?.LevelUpStartTime,
                    LevelUpFinishedTime = source.CurrentResearchTime?.LevelUpFinishedTime,
                    ResearchTimeToLevelUp = CloneResearchTime(source.CurrentResearchTime?.ResearchTimeToLevelUp)
                }
            };
        }

        private static ResearchTime CloneResearchTime(ResearchTime researchTime)
        {
            if (researchTime == null)
            {
                return new ResearchTime();
            }

            return new ResearchTime
            {
                days = researchTime.days,
                hours = researchTime.hours,
                minutes = researchTime.minutes,
                seconds = researchTime.seconds
            };
        }
    }
}
