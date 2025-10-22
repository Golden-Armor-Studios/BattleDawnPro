
using System;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;
using UnityEngine.UIElements;
using System.IO;
using System.Collections;
using System.Collections.Generic;

namespace Game {
    public static class GameConfig {
        public static List< Buff> buffs;
        public static List<Attribute> attributes;
        public static List<Research> researches;
        public delegate void getattributesCallback();
        public delegate void getBuffsCallback();
        public delegate void getResearchesCallback();

        public static async void getAttributes(getattributesCallback callback) {
            DB.Default.Init();
            if (DB.Default.gameConfig == null) {
                Debug.LogError("Firestore is not initialised. Sign in before loading attributes.");
                callback?.Invoke();
                return;
            }
            QuerySnapshot snapshot = await DB.Default.gameConfig.Document("AttributeConfig").Collection("Attributes").GetSnapshotAsync();
            attributes = new List<Attribute>();
                foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
                {
                    Attribute attribute = documentSnapshot.ConvertTo<Attribute>();
                    attribute.Id = documentSnapshot.Id;
                    attributes.Add(attribute);
                }
            callback();
        }

        public static async void getBuffs(getBuffsCallback callback) {
            DB.Default.Init();
            if (DB.Default.gameConfig == null) {
                Debug.LogError("Firestore is not initialised. Sign in before loading buffs.");
                callback?.Invoke();
                return;
            }
            QuerySnapshot snapshot = await DB.Default.gameConfig.Document("AttributeConfig").Collection("Buffs").GetSnapshotAsync();
            buffs = new List<Buff>();
                foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
                {
                    Buff buff = documentSnapshot.ConvertTo<Buff>();
                    buff.ResearchTimeToComplete = new System.TimeSpan(buff.ResearchTimeToLevelUp.days, buff.ResearchTimeToLevelUp.hours, buff.ResearchTimeToLevelUp.minutes, buff.ResearchTimeToLevelUp.seconds);
                    buff.Id = documentSnapshot.Id;
                    buffs.Add(buff);
                }
            callback();
        }

        public static async void getResearches(getResearchesCallback callback) {
            DB.Default.Init();
            if (DB.Default.gameConfig == null) {
                Debug.LogError("Firestore is not initialised. Sign in before loading researches.");
                callback?.Invoke();
                return;
            }
            QuerySnapshot snapshot = await DB.Default.gameConfig.Document("AttributeConfig").Collection("Researches").GetSnapshotAsync();
            researches = new List<Research>();
                foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
                {
                    Research research = documentSnapshot.ConvertTo<Research>();
                    research.ResearchTimeToComplete = new System.TimeSpan(research.ResearchTimeToLevelUp.days, research.ResearchTimeToLevelUp.hours, research.ResearchTimeToLevelUp.minutes, research.ResearchTimeToLevelUp.seconds);
                    research.Id = documentSnapshot.Id;
                    researches.Add(research);
                }
            callback();
        }
    }

    [FirestoreData]
    public class Attribute {
        [FirestoreProperty]
        public string Id { get; set; }
        [FirestoreProperty]
        public string AttributeName { get; set; }
        [FirestoreProperty]
        public string AttributeKey { get; set; }
        [FirestoreProperty]
        public float AttributeFloat { get; set; }
        [FirestoreProperty]
        public AttributeTypeEnum AttributeType { get; set; }
        public enum AttributeTypeEnum
        {
            Buff,
            Debuff,
            HomefieldAdvantage,
            DurationInMinutes,
            DurationInHours,
            DurationInDays,
        }
        [FirestoreProperty]
        public float EffectValue { get; set; }
        

        public async void SaveAttribute() {
            DB.Default.Init();
            CollectionReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Attributes");
            await docref.AddAsync(this);
        }

        public async void UpdateAttribute() {
            if (string.IsNullOrEmpty(Id)) {
                Debug.LogWarning("Cannot update attribute without an Id.");
                return;
            }
            DB.Default.Init();
            DocumentReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Attributes").Document(Id);
            await docref.SetAsync(this);
        }

        public async void DeleteAttribute() {
            DB.Default.Init();
            DocumentReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Attributes").Document(Id);
            await docref.DeleteAsync();
        }
    }

    [FirestoreData]
    public class Buff {
        [FirestoreProperty]
        public string Id{ get; set; }
        [FirestoreProperty]
        public string BuffName{ get; set; } = "";
        private List<Attribute> _attributeList = new List<Attribute>();
        [FirestoreProperty]
        public string BuffDescription{ get; set; }
        [FirestoreProperty]
        public string SpritePath{ get; set; }
        [FirestoreProperty]
        public string ResearchStartTime { get; set; }
        [FirestoreProperty]
        public string ResearchFinishedTime { get; set; }
        public TimeSpan ResearchTimeToComplete { get; set; } = new System.TimeSpan(0, 0, 0, 0);
        [FirestoreProperty]
        public CurrentResearchTime CurrentResearchTime{ get; set; }
        [FirestoreProperty]
        public ResearchTime ResearchTimeToLevelUp { get; set; } = new ResearchTime();

        public event Action<List<Attribute>> AttributeListChanged;

        [FirestoreProperty]
        public List<Attribute> attributeList {
            get => _attributeList;
            set
            {
                if (_attributeList != value)
                {
                    _attributeList = value;
                    AttributeListChanged?.Invoke(_attributeList);
                }
            }
        }

        public async void SaveBuff() {
            DB.Default.Init();
            CurrentResearchTime = new CurrentResearchTime();
            CurrentResearchTime.ResearchTimeToLevelUp.days = ResearchTimeToComplete.Days;
            CurrentResearchTime.ResearchTimeToLevelUp.hours = ResearchTimeToComplete.Hours;
            CurrentResearchTime.ResearchTimeToLevelUp.minutes = ResearchTimeToComplete.Minutes;
            CurrentResearchTime.ResearchTimeToLevelUp.seconds = ResearchTimeToComplete.Seconds;
            CollectionReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Buffs");
            await docref.AddAsync(this);
        }

        public async void UpdateBuff() {
            DB.Default.Init();
            CurrentResearchTime = new CurrentResearchTime();
            CurrentResearchTime.ResearchTimeToLevelUp.days = ResearchTimeToComplete.Days;
            CurrentResearchTime.ResearchTimeToLevelUp.hours = ResearchTimeToComplete.Hours;
            CurrentResearchTime.ResearchTimeToLevelUp.minutes = ResearchTimeToComplete.Minutes;
            CurrentResearchTime.ResearchTimeToLevelUp.seconds = ResearchTimeToComplete.Seconds;
            DocumentReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Buffs").Document(Id);
            await docref.SetAsync(this);
        }

        public async void DeleteBuff() {
            DB.Default.Init();
            // DocumentReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Buffs");
            // await docref.DeleteAsync();
        }
    }

    [FirestoreData]
    public class CurrentResearchTime {
        [FirestoreProperty]
        public string LevelUpStartTime { get; set; }
        [FirestoreProperty]
        public string LevelUpFinishedTime { get; set; }
        [FirestoreProperty]
        public ResearchTime ResearchTimeToLevelUp { get; set; } = new ResearchTime();
    }

    [FirestoreData]
    public class Research {

        [FirestoreProperty]
        public string Id { get; set; }
        [FirestoreProperty]
        public string BuffCompletedSpritePath{ get; set; }
        [FirestoreProperty]
        public string ResearchName { get; set; }
        [FirestoreProperty]
        public string ResearchDescription { get; set; }
        [FirestoreProperty]
        public int ResearchLevel { get; set; } = 0;
        public TimeSpan ResearchTimeToComplete { get; set; } = new System.TimeSpan(0, 0, 0, 0);
        [FirestoreProperty]
        public CurrentResearchTime CurrentResearchTime{ get; set; }
        [FirestoreProperty]
        public Buff[] BuffList { get; set; } = new Buff[4];
        [FirestoreProperty]
        public ResearchTypeEnum ResearchType { get; set; } = ResearchTypeEnum.Research;
        [FirestoreProperty]
        public ResearchTime ResearchTimeToLevelUp { get; set; } = new ResearchTime();
        
        [FirestoreProperty]
        public bool ReadyToLevelUp { get; set; } = false;
        
        public enum ResearchTypeEnum
        {
            Research,
            Military,
            Base,
            Commander
        }

        public async void SaveResearch() {
            DB.Default.Init();
            CurrentResearchTime = new CurrentResearchTime();
            CurrentResearchTime.ResearchTimeToLevelUp.days = ResearchTimeToComplete.Days;
            CurrentResearchTime.ResearchTimeToLevelUp.hours = ResearchTimeToComplete.Hours;
            CurrentResearchTime.ResearchTimeToLevelUp.minutes = ResearchTimeToComplete.Minutes;
            CurrentResearchTime.ResearchTimeToLevelUp.seconds = ResearchTimeToComplete.Seconds;
            CollectionReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Researches");
            await docref.AddAsync(this);
        }

        public async void UpdateResearch() {
            DB.Default.Init();
            CurrentResearchTime = new CurrentResearchTime();
            CurrentResearchTime.ResearchTimeToLevelUp.days = ResearchTimeToComplete.Days;
            CurrentResearchTime.ResearchTimeToLevelUp.hours = ResearchTimeToComplete.Hours;
            CurrentResearchTime.ResearchTimeToLevelUp.minutes = ResearchTimeToComplete.Minutes;
            CurrentResearchTime.ResearchTimeToLevelUp.seconds = ResearchTimeToComplete.Seconds;
            DocumentReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Researches").Document(Id);
            await docref.SetAsync(this);
        }

        public async void DeleteResearch() {
            if (string.IsNullOrEmpty(Id)) {
                Debug.LogWarning("Cannot delete research without an Id.");
                return;
            }
            DB.Default.Init();
            DocumentReference docref = DB.Default.gameConfig.Document("AttributeConfig").Collection("Researches").Document(Id);
            await docref.DeleteAsync();
        }
        
    }
    
    [FirestoreData]
    public class ResearchTime {
        [FirestoreProperty]
        public int days{ get; set; } = 0;
        [FirestoreProperty]
        public int hours{ get; set; } = 0;
        [FirestoreProperty]
        public int minutes{ get; set; } = 0;
        [FirestoreProperty]
        public int seconds{ get; set; } = 0;
    }

    [Serializable]
    [FirestoreData]
    public class Profile {
        [FirestoreProperty]
        public ResearchLevels ResearchLevels{ get; set; } = new ResearchLevels();
        [FirestoreProperty]
        public CurrentResearch CurrentResearch{ get; set; } = new CurrentResearch();
    }

    [Serializable]
    [FirestoreData]
    public class ResearchLevels {
        [FirestoreProperty]
        public int Research{ get; set; } = 1;
        [FirestoreProperty]
        public int Base{ get; set; } = 1;
        [FirestoreProperty]
        public int Military{ get; set; } = 1;
        [FirestoreProperty]
        public int Comander{ get; set; } = 1;
    }

    [Serializable]
    [FirestoreData]
    public class CurrentResearch {
        [FirestoreProperty]
        public int IsResearching { get; set; } = 0;
        [FirestoreProperty]
        public int ResearchesAtATime { get; set; } = 1;
        [FirestoreProperty]
        public List<Buff> CurrentResearches { get; set; } = new List<Buff>();
        [FirestoreProperty]
        public string CurrentResearchStarted { get; set; } = string.Empty;
        [FirestoreProperty]
        public string CurrentResearchFinished { get; set; } = string.Empty;
        [FirestoreProperty]
        public Research Research { get; set; } = new Research();
    }
}
