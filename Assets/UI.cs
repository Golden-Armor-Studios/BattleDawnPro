using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;
using Auth;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI; 
using System.Text;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using System.Linq;  
using DB;
using Game;

namespace  UI {
    public static class ResearchUI {

        private static GameObject MainContentGameObject;
        private static GameObject ResearchBackgroundGameObject;
        private static GameObject MainNavigationGameObject;
        private static Transform ResearchButtonsGameObjectTransform;
        private static Transform MainNavigationGameObjectTransform;

        private static GameObject ResearchButtonGameObject;
        private static string ResearchButtonDefaultSprite;
        private static string ResearchButtonCurrentSprite;
        private static Button ResearchButton;
        private static Image ResearchImg;

        private static GameObject BaseButtonGameObject;
        private static string BaseButtonDefaultSprite;
        private static string BaseButtonCurrentSprite;
        private static Button BaseButton;
        private static Image BaseImg;

        private static GameObject MilitaryButtonGameObject;
        private static string MilitaryButtonDefaultSprite;
        private static string MilitaryButtonCurrentSprite;
        private static Button MilitaryButton;
        private static Image MilitaryImg;

        private static GameObject CommanderButtonGameObject;
        private static string CommanderButtonDefaultSprite;
        private static string CommanderButtonCurrentSprite;
        private static Button CommanderButton;
        private static Image CommanderImg;



        private static GameObject CurrentButtonClicked;
        public static string ResearchType;



        private static Profile Profile;


        public static bool IsActive = true;

        private static Research Research;
        
        public static bool ShowResearchUI = false;
        public static bool DidShowResearchUI = false;
        public static GameObject ResearchRankTextGameObject;
        public static TMP_Text ResearchRankText;
        public static event Action ResearchLevelsChanged;
        

        private static int ResearchLevel;

        public static async void InitUI () {

            MainContentGameObject = GameObject.Find("MainContent");

            Transform ResearchBackgroundGameObjectTransform = MainContentGameObject.transform.Find("ResearchBackground");
            ResearchBackgroundGameObject = ResearchBackgroundGameObjectTransform.gameObject;

            MainNavigationGameObjectTransform = MainContentGameObject.transform.Find("MainNavigation");
            MainNavigationGameObject = MainNavigationGameObjectTransform.gameObject;

            ResearchButtonsGameObjectTransform = MainContentGameObject.transform.Find("ResearchBackground/ResearchButtonGroup/ResearchButtons");
            MainNavigationGameObject = MainNavigationGameObjectTransform.gameObject;

            SetResearch("Default");

            CollectionReference gameConfig = DB.Default.gameConfig.Document("AttributeConfig").Collection("Researches");
            Query query = gameConfig.WhereEqualTo("ResearchLevel", 0).Limit(1);
            QuerySnapshot snapshot = await query.GetSnapshotAsync();
            DocumentSnapshot ResearchDoc = snapshot.Documents.FirstOrDefault();
            Research = ResearchDoc.ConvertTo<Research>();

            MakeButtons(MainNavigationGameObjectTransform, true);



        }

        public static void Render () {
            
        }

        public static void MakeButtons(Transform ParentTransform, bool isDefault) {
            ResearchButtonGameObject = new GameObject();
            RectTransform ResearchButtonRT = ResearchButtonGameObject.AddComponent<RectTransform>();
            ResearchButtonRT.sizeDelta = new Vector2(160f, 160f);
            ResearchButtonGameObject.name = isDefault == true ? "Default" : Research.BuffList[0].Id; // turinary for default vs id
            ResearchButtonGameObject.tag = "Research";
            ResearchButton = ResearchButtonGameObject.AddComponent<Button>();
            ResearchImg = ResearchButtonGameObject.AddComponent<Image>();
            ResearchImg.sprite = Resources.Load<Sprite>(Research.BuffList[0].SpritePath);
            ResearchButtonGameObject.transform.SetParent(ParentTransform);
            ResearchButton.onClick.AddListener(delegate { ResearchButtonClicked(ResearchButtonGameObject); });
            ResearchButtonRT.localPosition = Vector3.zero;
            ResearchButtonRT.localScale = Vector3.one;

            BaseButtonGameObject = new GameObject();
            RectTransform BaseButtonRT = BaseButtonGameObject.AddComponent<RectTransform>();
            BaseButtonRT.sizeDelta = new Vector2(160f, 160f);
            BaseButtonGameObject.name = isDefault == true ? "Default" : Research.BuffList[1].Id;
            BaseButtonGameObject.tag = "Base";
            BaseButton = BaseButtonGameObject.AddComponent<Button>();
            BaseImg = BaseButtonGameObject.AddComponent<Image>();
            BaseImg.sprite = Resources.Load<Sprite>(Research.BuffList[1].SpritePath);
            BaseButtonGameObject.transform.SetParent(ParentTransform);
            BaseButton.onClick.AddListener(delegate { ResearchButtonClicked(BaseButtonGameObject); });
            BaseButtonRT.localPosition = Vector3.zero;
            BaseButtonRT.localScale = Vector3.one;

            MilitaryButtonGameObject = new GameObject();
            RectTransform MilitaryButtonRT = MilitaryButtonGameObject.AddComponent<RectTransform>();
            MilitaryButtonRT.sizeDelta = new Vector2(160f, 160f);
            MilitaryButtonGameObject.name = isDefault == true ? "Default" : Research.BuffList[2].Id;
            MilitaryButtonGameObject.tag = "Military";
            MilitaryButton = MilitaryButtonGameObject.AddComponent<Button>();
            MilitaryImg = MilitaryButtonGameObject.AddComponent<Image>();
            MilitaryImg.sprite = Resources.Load<Sprite>(Research.BuffList[2].SpritePath);
            MilitaryButtonGameObject.transform.SetParent(ParentTransform);
            MilitaryButton.onClick.AddListener(delegate { ResearchButtonClicked(MilitaryButtonGameObject); });
            MilitaryButtonRT.localPosition = Vector3.zero;
            MilitaryButtonRT.localScale = Vector3.one;
            
            CommanderButtonGameObject = new GameObject();
            RectTransform CommanderButtonRT = CommanderButtonGameObject.AddComponent<RectTransform>();
            CommanderButtonRT.sizeDelta = new Vector2(160f, 160f);
            CommanderButtonGameObject.name = isDefault == true ? "Default" : Research.BuffList[3].Id;
            CommanderButtonGameObject.tag = "Commander";
            CommanderButton = CommanderButtonGameObject.AddComponent<Button>();
            CommanderImg = CommanderButtonGameObject.AddComponent<Image>();
            CommanderImg.sprite = Resources.Load<Sprite>(Research.BuffList[3].SpritePath);
            CommanderButtonGameObject.transform.SetParent(ParentTransform);
            CommanderButton.onClick.AddListener(delegate { ResearchButtonClicked(CommanderButtonGameObject); });
            CommanderButtonRT.localPosition = Vector3.zero;
            CommanderButtonRT.localScale = Vector3.one;
        }

        private static void ShowResearchUIUpdate() {

        }

        private static void GetButtonsOnScreen(string GameObjectName = "Default") {
            if (Research.BuffList.Any(research => research.Id == GameObjectName)) {


                ResearchButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[0].Id).gameObject;
                ResearchImg = ResearchButtonGameObject.AddComponent<Image>();
                ResearchImg.sprite = Resources.Load<Sprite>(ResearchButtonCurrentSprite); 

                BaseButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[1].Id).gameObject;
                BaseImg = BaseButtonGameObject.AddComponent<Image>();
                BaseImg.sprite = Resources.Load<Sprite>(BaseButtonCurrentSprite);

                MilitaryButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[2].Id).gameObject;
                MilitaryImg = MilitaryButtonGameObject.AddComponent<Image>();
                MilitaryImg.sprite = Resources.Load<Sprite>(MilitaryButtonCurrentSprite);

                CommanderButtonGameObject = MainContentGameObject.transform.Find(Research.BuffList[3].Id).gameObject;
                CommanderImg = CommanderButtonGameObject.AddComponent<Image>();
                CommanderImg.sprite = Resources.Load<Sprite>(CommanderButtonCurrentSprite);
            }
            if (GameObjectName == "Default") { // <-------- includes default

                ResearchButtonGameObject = MainNavigationGameObject.transform.Find("Research-Default").gameObject;
                ResearchImg = ResearchButtonGameObject.AddComponent<Image>();
                ResearchImg.sprite = Resources.Load<Sprite>(ResearchButtonDefaultSprite); 

                BaseButtonGameObject = MainNavigationGameObject.transform.Find("Base-Default").gameObject;
                BaseImg = BaseButtonGameObject.AddComponent<Image>();
                BaseImg.sprite = Resources.Load<Sprite>(BaseButtonDefaultSprite);

                BaseButtonGameObject = MainNavigationGameObject.transform.Find("Military-Default").gameObject;
                MilitaryImg = MilitaryButtonGameObject.AddComponent<Image>();
                MilitaryImg.sprite = Resources.Load<Sprite>(MilitaryButtonDefaultSprite);

                BaseButtonGameObject = MainNavigationGameObject.transform.Find("Commander-Default").gameObject;
                CommanderImg = CommanderButtonGameObject.AddComponent<Image>();
                CommanderImg.sprite = Resources.Load<Sprite>(CommanderButtonDefaultSprite);
            }
        }
        
        private static async void ResearchButtonClicked(GameObject button)
        {
            // GetButtonsOnScreen(GameObjectName);
            if (button.tag == "Research" && button.name == "Default") { 

                SetResearch(button.tag);


                DeleteAllChildren(MainNavigationGameObjectTransform);
                ShowResearchBackground(true);
                Debug.Log(ResearchLevel);
                MakeButtons(ResearchButtonsGameObjectTransform , false);




                // CurrentResearchTime CurrentResearchTime = new CurrentResearchTime();
                // CurrentResearchTime.LevelUpStartTime = "0";
                // CurrentResearchTime.LevelUpFinishedTime = "0";
                // CurrentResearchTime.ResearchTimeToLevelUp = new ResearchTime();

                // DocumentReference GameProfile = DB.Default.GameProfiles.Document(Auth.User.user.UserId);
                // await GameProfile.UpdateAsync("CurrentResearchTime", CurrentResearchTime);

                // save to the profile
            } 
            
            if (button.tag != "Base") { 
            }

            if (button.tag != "Military") { 
            }

            if (button.tag != "Commander") { 
            }
            
            else {
                ShowResearchBackground(false);
            }

        }

        private static void ShowResearchBackground(bool show) {
            if (show == true) {
                ResearchBackgroundGameObject.SetActive(true);
                MainNavigationGameObject.SetActive(false);
            } else {
                ResearchBackgroundGameObject.SetActive(false);
                MainNavigationGameObject.SetActive(true);
            }
        }

        private static async void SetResearch(string researchtype) {
            DocumentReference GameProfileRef = DB.Default.GameProfiles.Document(Auth.User.user.UserId);
            DocumentSnapshot GameProfileDoc = await GameProfileRef.GetSnapshotAsync();
            Profile = GameProfileDoc.ConvertTo<Profile>();
            Debug.Log(researchtype);
            switch (researchtype)
            {
                case "Research":
                    Debug.Log(Profile.ResearchLevels.Research);
                    ResearchLevel = Profile.ResearchLevels.Research;
                    Debug.Log(ResearchLevel);
                    break;
                case "Base":
                    ResearchLevel = Profile.ResearchLevels.Base;
                    break;
                case "Military":
                    ResearchLevel = Profile.ResearchLevels.Military;
                    break;
                case "Commander":
                    ResearchLevel = Profile.ResearchLevels.Comander;
                    break;
                default: {
                    ResearchLevel = 0;
                    break;
                }
            }
            CollectionReference gameConfig = DB.Default.gameConfig.Document("AttributeConfig").Collection("Researches");
            Query query = gameConfig.WhereEqualTo("ResearchLevel", ResearchLevel).WhereEqualTo("ResearchType", 0).Limit(1); // make this use the enum
            QuerySnapshot snapshot = await query.GetSnapshotAsync();
            DocumentSnapshot ResearchDoc = snapshot.Documents.FirstOrDefault();
            Research = ResearchDoc.ConvertTo<Research>();
        }

        public static void DeleteAllChildren(Transform parent) {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            GameObject.Destroy(parent.GetChild(i).gameObject);
        }
    }

    }
}


