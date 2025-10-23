using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.AnimatedValues;
using UnityEditor.UIElements;
using UnityEngine.Events;
using UnityEngine.Tilemaps;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using System;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using TMPro;
using System.IO;
using Game;
using EditorTools;

public class UI: EditorWindow
{

    private static VisualElement ViElement;
    private static ScrollView AttributesScrollView;
    private static Foldout AttributeFoldout;
    private static ScrollView BuffsScrollView;
    private static Foldout BuffsFoldout;
    private static ScrollView BuffsAttributesScrollView;
    private static VisualElement panel;
    private static Foldout ResearchsFoldout;
    private static ScrollView ResearchsScrollView;
    private static Foldout MapsFoldout;
    private static ScrollView MapsScrollView;
    private static Foldout MapPaletteFoldout;
    private static CreateGrassAndWaterTiles.GeneratorState mapPaletteGeneratorState = new CreateGrassAndWaterTiles.GeneratorState();
    private static List<Foldout> foldouts = new List<Foldout>();
    private static Dictionary<Research.ResearchTypeEnum, bool> researchTypeFoldoutState = new Dictionary<Research.ResearchTypeEnum, bool>();
    private static Dictionary<string, bool> researchFoldoutState = new Dictionary<string, bool>();
    private static Label firebaseStatusLabel;
    private static Label githubUsernameLabel;
    private static VisualElement githubAvatarElement;
    private static Texture2D githubAvatarTexture;
    private static Foldout firebaseFoldoutRef;
    private static Button migrateDbButton;
    private static bool isMigrating;
    private static bool hasInitialDataLoad;
    private static TextField mapNameField;
    private static SliderInt mapSizeSlider;
    private static Label mapStatusLabel;
    private static ListenerRegistration mapsListener;
    private static ObjectField mapPlanetSurfaceField;
    private static string mapPlanetSurfaceResourcePath = "MapPallet/Grass/Ocean";

    [MenuItem("Game Config/RunTime GameConfig")]
    static void Init()
    {  
        UI wnd = GetWindow<UI>();
        wnd.titleContent = new GUIContent("RunTime GameConfig");
        wnd.Show();
        
    }


    public void CreateGUI()
    {

        ViElement = rootVisualElement;
        ViElement.style.minHeight = 200;      
        ViElement.style.display = DisplayStyle.Flex; 
        ViElement.style.flexDirection = FlexDirection.Column;
        ViElement.style.justifyContent = Justify.FlexStart;

        // Account summary block
        var accountRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.Center,
                marginBottom = 8,
                backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f),
                paddingLeft = 10,
                paddingRight = 10,
                paddingTop = 8,
                paddingBottom = 8,
                borderTopLeftRadius = 6,
                borderTopRightRadius = 6,
                borderBottomLeftRadius = 6,
                borderBottomRightRadius = 6
            }
        };

        githubAvatarElement = new VisualElement { name = "github-avatar" };
        githubAvatarElement.style.width = 48;
        githubAvatarElement.style.height = 48;
        githubAvatarElement.style.borderTopLeftRadius = githubAvatarElement.style.borderTopRightRadius =
            githubAvatarElement.style.borderBottomLeftRadius = githubAvatarElement.style.borderBottomRightRadius = 24;
        githubAvatarElement.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        githubAvatarElement.style.marginRight = 10;
        accountRow.Add(githubAvatarElement);

        var accountInfoColumn = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.Center
            }
        };

        githubUsernameLabel = new Label("Not logged in")
        {
            style =
            {
                unityFontStyleAndWeight = FontStyle.Bold,
                fontSize = 14,
                whiteSpace = WhiteSpace.Normal
            }
        };
        accountInfoColumn.Add(githubUsernameLabel);

        firebaseStatusLabel = new Label
        {
            style =
            {
                color = new Color(0.7f, 0.7f, 0.7f, 1f),
                marginTop = 2,
                whiteSpace = WhiteSpace.Normal
            }
        };
        accountInfoColumn.Add(firebaseStatusLabel);

        accountRow.Add(accountInfoColumn);
        ViElement.Add(accountRow);

        firebaseFoldoutRef = new Foldout { text = "Firebase" };
        ViElement.Add(firebaseFoldoutRef);

        var loginButton = new Button(() =>
        {
            ShowGitHubLoginModal();
        }) { text = "Login" };
        firebaseFoldoutRef.Add(loginButton);

        var logoutButton = new Button(() =>
        {
            Auth.User.Logout();
            UpdateFirebaseStatus();
        }) { text = "Sign Out" };
        firebaseFoldoutRef.Add(logoutButton);

        migrateDbButton = new Button(RunMigrationAsync) { text = "Migrate Firestore Data" };
        migrateDbButton.tooltip = "Populate the battledawnpro Firestore database with the baseline game configuration.";
        migrateDbButton.SetEnabled(false);
        firebaseFoldoutRef.Add(migrateDbButton);

        UpdateFirebaseStatus();

        AttributeFoldout = new Foldout();
        AttributeFoldout.text = "Attributes";
        ViElement.Add(AttributeFoldout);

        Button addAttributeButton = new Button(ShowAddAttributeModal) { text = "Add Attribute" };
        AttributeFoldout.Add(addAttributeButton);

        AttributesScrollView = new ScrollView();
        AttributeFoldout.Add(AttributesScrollView);

        BuffsFoldout = new Foldout();
        BuffsFoldout.text = "Buffs";
        ViElement.Add(BuffsFoldout);

        Button addBuffButton = new Button(ShowAddBuffModal) { text = "Add Buff" };
        BuffsFoldout.Add(addBuffButton);

        BuffsScrollView = new ScrollView();
        BuffsFoldout.Add(BuffsScrollView);

        ResearchsFoldout = new Foldout();
        ResearchsFoldout.text = "Researches";
        ViElement.Add(ResearchsFoldout);

        Button addResearchButton = new Button(ShowAddResearchModal) { text = "Add Research" };
        ResearchsFoldout.Add(addResearchButton);

        ResearchsScrollView = new ScrollView();
        ResearchsFoldout.Add(ResearchsScrollView);

        MapsFoldout = new Foldout { text = "Maps" };
        ViElement.Add(MapsFoldout);

        var mapControls = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                marginBottom = 8
            }
        };

        mapNameField = new TextField("Planet Name") { value = string.Empty };
        mapNameField.style.marginBottom = 4;
        mapControls.Add(mapNameField);

        mapSizeSlider = new SliderInt
        {
            label = "Tiles Wide",
            lowValue = 100,
            highValue = 1000,
            showInputField = true
        };
        mapSizeSlider.value = 250;
        mapControls.Add(mapSizeSlider);

        mapPlanetSurfaceField = new ObjectField("Planet Surface Sprite")
        {
            objectType = typeof(Sprite),
            allowSceneObjects = false
        };
        mapPlanetSurfaceField.value = LoadSpriteFromResources(mapPlanetSurfaceResourcePath);
        mapPlanetSurfaceField.RegisterValueChangedCallback(evt => HandlePlanetSurfaceSpriteChanged(evt.newValue as Sprite));
        mapControls.Add(mapPlanetSurfaceField);

        var createMapButton = new Button(CreateMapFromUI) { text = "Create Map" };
        mapControls.Add(createMapButton);

        mapStatusLabel = new Label
        {
            style =
            {
                whiteSpace = WhiteSpace.Normal,
                color = new Color(0.7f, 0.7f, 0.7f, 1f)
            }
        };
        mapControls.Add(mapStatusLabel);

        MapsFoldout.Add(mapControls);

        MapsScrollView = new ScrollView
        {
            style =
            {
                height = 300,
                backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f)
            }
        };
        MapsFoldout.Add(MapsScrollView);

        MapPaletteFoldout = new Foldout { text = "Map Pallet" };
        ViElement.Add(MapPaletteFoldout);
        MapPaletteFoldout.Add(CreateGrassAndWaterTiles.CreateGeneratorUI(mapPaletteGeneratorState));

        TryRequestGameConfigData();

    }

    private void ShowAddAttributeModal()
    {
        Game.Attribute attribute = new Game.Attribute();
        ShowToolkitModal(attribute, true);
    }

    private void ShowAddBuffModal()
    {
        Buff buff = new Buff();
        ShowToolkitModal(buff, true);
    }

    private void ShowAddResearchModal()
    {
        Research research = new Research();
        EnsureResearchSlots(research);

        var veil = new VisualElement { name = "add-research-modal-veil" };
        veil.style.position = Position.Absolute;
        veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
        veil.style.backgroundColor = new Color(0,0,0,0.35f);
        veil.pickingMode = PickingMode.Position;

        var modalPanel = new VisualElement { name = "add-research-panel" };
        modalPanel.style.alignSelf = Align.Center;
        modalPanel.style.width = 520;
        modalPanel.style.minHeight = 360;
        modalPanel.style.marginLeft = modalPanel.style.marginRight = 20;
        modalPanel.style.marginTop = 40;
        modalPanel.style.paddingLeft = modalPanel.style.paddingRight = 14;
        modalPanel.style.paddingTop = modalPanel.style.paddingBottom = 14;
        modalPanel.style.backgroundColor = new Color(0.18f,0.18f,0.18f,1f);
        modalPanel.style.borderTopLeftRadius = modalPanel.style.borderTopRightRadius =
            modalPanel.style.borderBottomLeftRadius = modalPanel.style.borderBottomRightRadius = 6;

        var buttonRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceBetween,
                marginBottom = 12
            }
        };

        var cancelButton = new Button(() => ViElement.Remove(veil)) { text = "Cancel" };
        var saveButton = new Button(() =>
        {
            ViElement.Remove(veil);
            research.SaveResearch();
            GameConfig.getResearches(() => { renderResearchList(false, new Research()); });
        }) { text = "Create Research" };

        buttonRow.Add(cancelButton);
        buttonRow.Add(saveButton);
        modalPanel.Add(buttonRow);

        var nameField = new TextField("Research Name") { value = research.ResearchName };
        nameField.RegisterValueChangedCallback(evt => research.ResearchName = evt.newValue);
        modalPanel.Add(nameField);

        var descriptionField = new TextField("Research Description")
        {
            value = research.ResearchDescription,
            multiline = true
        };
        descriptionField.RegisterValueChangedCallback(evt => research.ResearchDescription = evt.newValue);
        modalPanel.Add(descriptionField);

        var typeField = new EnumField("Research Type", research.ResearchType);
        typeField.RegisterValueChangedCallback(evt =>
        {
            research.ResearchType = (Research.ResearchTypeEnum)evt.newValue;
        });
        modalPanel.Add(typeField);

        var levelField = new IntegerField("Research Level") { value = research.ResearchLevel };
        levelField.RegisterValueChangedCallback(evt => research.ResearchLevel = evt.newValue);
        modalPanel.Add(levelField);

        var durationLabel = new Label("Set the research level-up duration:");
        durationLabel.style.marginTop = 6;
        modalPanel.Add(durationLabel);
        MakeResearchDurationSelector(research, modalPanel);

        veil.Add(modalPanel);
        ViElement.Add(veil);
    }

    private void ShowGitHubLoginModal()
    {
        var veil = new VisualElement { name = "github-login-veil" };
        veil.style.position = Position.Absolute;
        veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
        veil.style.backgroundColor = new Color(0,0,0,0.4f);
        veil.pickingMode = PickingMode.Position;

        var panel = new VisualElement { name = "github-login-panel" };
        panel.style.alignSelf = Align.Center;
        panel.style.width = 420;
        panel.style.minHeight = 220;
        panel.style.marginLeft = panel.style.marginRight = 20;
        panel.style.marginTop = 40;
        panel.style.paddingLeft = panel.style.paddingRight = 16;
        panel.style.paddingTop = panel.style.paddingBottom = 16;
        panel.style.backgroundColor = new Color(0.18f,0.18f,0.18f,1f);
        panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius =
            panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 6;

        var header = new Label("Login with Dev Token");
        header.style.unityFontStyleAndWeight = FontStyle.Bold;
        header.style.marginBottom = 8;
        panel.Add(header);

        var instructions = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                marginBottom = 10
            }
        };
        instructions.Add(CreateInstructionStep("1", "Request a developer token from ", "the Dev Tools page", "https://goldenarmorstudio.art/dev-tools", "."));
        instructions.Add(CreateInstructionStep("2", "Need access? Apply on ", "the developer signup form", "https://goldenarmorstudio.art/join-team", "."));
        instructions.Add(CreateInstructionStep("3", "Have questions? Join ", "the community Discord", "https://goldenarmorstudio.art/community", " for support and updates."));
        instructions.Add(CreateInstructionStep("4", "Paste the token below and click Login. We'll store it securely for your next session."));
        panel.Add(instructions);

        var tokenField = new TextField("Dev Token") { isPasswordField = true };
        tokenField.style.marginBottom = 12;
        panel.Add(tokenField);

        var buttonRow = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                justifyContent = Justify.SpaceBetween
            }
        };

        var cancelButton = new Button(() => ViElement.Remove(veil)) { text = "Cancel" };
        var statusMessage = new Label();
        statusMessage.style.whiteSpace = WhiteSpace.Normal;
        statusMessage.style.marginTop = 8;
        statusMessage.style.color = new Color(0.9f, 0.3f, 0.3f, 1f);

        Button loginButton = null;
        loginButton = new Button(async () =>
        {
            string token = tokenField.value?.Trim();
            if (string.IsNullOrEmpty(token)) {
                statusMessage.text = "Token cannot be empty.";
                return;
            }

            loginButton.SetEnabled(false);
            statusMessage.text = "Signing in...";
            statusMessage.style.color = new Color(0.8f, 0.8f, 0.3f, 1f);
            try {
                var user = await Auth.User.LoginWithGitHubAsync(token);
                if (user != null) {
                    statusMessage.text = "Sign-in successful.";
                    statusMessage.style.color = new Color(0.2f, 0.8f, 0.2f, 1f);
                    UpdateFirebaseStatus();
                    ViElement.Remove(veil);
                } else {
                    statusMessage.text = "GitHub sign-in failed. Check the Console for details.";
                    statusMessage.style.color = new Color(0.9f, 0.3f, 0.3f, 1f);
                    loginButton.SetEnabled(true);
                }
            } catch (Exception ex) {
                Debug.LogError(ex);
                statusMessage.text = "Unexpected error during sign-in.";
                statusMessage.style.color = new Color(0.9f, 0.3f, 0.3f, 1f);
                loginButton.SetEnabled(true);
            }
        }) { text = "Login" };

        buttonRow.Add(cancelButton);
        buttonRow.Add(loginButton);
        panel.Add(buttonRow);
        panel.Add(statusMessage);

        veil.Add(panel);
        ViElement.Add(veil);
    }

    private static void UpdateFirebaseStatus()
    {
        if (firebaseStatusLabel == null || firebaseFoldoutRef == null) {
            return;
        }

        var currentUser = Auth.User.user ?? (Auth.User.auth != null ? Auth.User.auth.CurrentUser : null);
        if (currentUser != null)
        {
            string dbName = DB.Default.DatabaseName;
            firebaseStatusLabel.text = $"Signed in as: {currentUser.Email} · DB: {dbName}";
            firebaseStatusLabel.style.color = new Color(0.2f, 0.8f, 0.2f, 1f);
            firebaseFoldoutRef.value = false;

            if (githubUsernameLabel != null)
            {
                string githubName = ResolveGitHubDisplayName(currentUser);
                githubUsernameLabel.text = string.IsNullOrEmpty(githubName) ? "GitHub account" : githubName;
            }

            SetAvatarAsync(currentUser.PhotoUrl);
            TryRequestGameConfigData();
        }
        else
        {
            string dbName = DB.Default.DatabaseName;
            firebaseStatusLabel.text = $"Not signed in to Firebase. DB: {dbName}";
            firebaseStatusLabel.style.color = new Color(0.9f, 0.3f, 0.3f, 1f);
            firebaseFoldoutRef.value = true;
            if (githubUsernameLabel != null)
            {
                githubUsernameLabel.text = "Not logged in";
            }
            ResetAvatarVisual();
            hasInitialDataLoad = false;
            ShowAuthRequiredMessages();
        }

        if (migrateDbButton != null)
        {
            bool canMigrate = currentUser != null && !isMigrating;
            migrateDbButton.SetEnabled(canMigrate);
        }
    }

    private static string ResolveGitHubDisplayName(Firebase.Auth.FirebaseUser currentUser)
    {
        if (currentUser == null) {
            return string.Empty;
        }

        string name = currentUser.DisplayName;
        try {
            foreach (var profile in currentUser.ProviderData)
            {
                if (!string.IsNullOrEmpty(profile?.ProviderId) && profile.ProviderId.Contains("github", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrEmpty(profile.DisplayName))
                    {
                        name = profile.DisplayName;
                    }
                    else if (!string.IsNullOrEmpty(profile.UserId))
                    {
                        name = profile.UserId;
                    }
                    break;
                }
            }
        } catch (Exception ex) {
            Debug.LogWarning($"Failed to resolve GitHub display name: {ex.Message}");
        }

        return name;
    }

    private static async void SetAvatarAsync(Uri photoUri)
    {
        if (githubAvatarElement == null) {
            return;
        }

        githubAvatarElement.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        githubAvatarElement.style.backgroundImage = null;

        if (githubAvatarTexture != null) {
            UnityEngine.Object.DestroyImmediate(githubAvatarTexture);
            githubAvatarTexture = null;
        }

        if (photoUri == null) {
            return;
        }

        try {
            using (var request = UnityWebRequestTexture.GetTexture(photoUri))
            {
                var op = request.SendWebRequest();
                while (!op.isDone)
                {
                    await System.Threading.Tasks.Task.Yield();
                }

#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogWarning($"Failed to download GitHub avatar: {request.error}");
                    return;
                }

                var texture = DownloadHandlerTexture.GetContent(request);
                githubAvatarTexture = texture;
                githubAvatarElement.style.backgroundImage = new StyleBackground(texture);
                githubAvatarElement.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            }
        } catch (Exception ex) {
            Debug.LogWarning($"Exception downloading GitHub avatar: {ex.Message}");
        }
    }

    private static void ResetAvatarVisual()
    {
        if (githubAvatarElement != null)
        {
            githubAvatarElement.style.backgroundImage = null;
            githubAvatarElement.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        }

        if (githubAvatarTexture != null)
        {
            UnityEngine.Object.DestroyImmediate(githubAvatarTexture);
            githubAvatarTexture = null;
        }
    }

    public static void MakeBuffDurationSelector(Buff buff, VisualElement panel)
        {
            var container = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart, marginRight = 4 } };
            List<string> dates = new List<string>();
            dates.Add("Day");
            dates.Add("Hour");
            dates.Add("Minutes");
            dates.Add("Seconds");


            foreach (string date in dates) {

                switch (date) {
                    case "Day": {
                        var dateContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Days");
                        dateContainer.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer.Add(field);
                        field.value = buff.ResearchTimeToComplete.Days;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Max(0, field.value);
                            buff.ResearchTimeToComplete = new System.TimeSpan(field.value, buff.ResearchTimeToComplete.Hours, buff.ResearchTimeToComplete.Minutes, buff.ResearchTimeToComplete.Seconds);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Max(0, field.value);
                            buff.ResearchTimeToComplete = new System.TimeSpan(field.value, buff.ResearchTimeToComplete.Hours, buff.ResearchTimeToComplete.Minutes, buff.ResearchTimeToComplete.Seconds);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer.Add(up);
                        dateContainer.Add(down);
                        container.Add(dateContainer);
                        break;
                    }
                    case "Hour": {
                        var dateContainer2 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Hours");
                        dateContainer2.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer2.Add(field);

                        field.value = buff.ResearchTimeToComplete.Hours;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Clamp(field.value, 0, 23);
                            buff.ResearchTimeToComplete = new System.TimeSpan(buff.ResearchTimeToComplete.Days, field.value, buff.ResearchTimeToComplete.Minutes, buff.ResearchTimeToComplete.Seconds);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Clamp(field.value, 0, 23);
                            buff.ResearchTimeToComplete = new System.TimeSpan(buff.ResearchTimeToComplete.Days, field.value, buff.ResearchTimeToComplete.Minutes, buff.ResearchTimeToComplete.Seconds);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer2.Add(up);
                        dateContainer2.Add(down);
                        container.Add(dateContainer2);
                        break;
                    }
                    case "Minutes": {
                        var dateContainer3 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Minutes");
                        dateContainer3.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer3.Add(field);

                        field.value = buff.ResearchTimeToComplete.Minutes;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            buff.ResearchTimeToComplete = new System.TimeSpan(buff.ResearchTimeToComplete.Days, buff.ResearchTimeToComplete.Hours, field.value, buff.ResearchTimeToComplete.Seconds);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            buff.ResearchTimeToComplete = new System.TimeSpan(buff.ResearchTimeToComplete.Days, buff.ResearchTimeToComplete.Hours, field.value, buff.ResearchTimeToComplete.Seconds);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer3.Add(up);
                        dateContainer3.Add(down);
                        container.Add(dateContainer3);
                        break;
                    }
                    case "Seconds": {
                        var dateContainer4 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Seconds");
                        dateContainer4.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer4.Add(field);

                        field.value = buff.ResearchTimeToComplete.Seconds;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            buff.ResearchTimeToComplete = new System.TimeSpan(buff.ResearchTimeToComplete.Days, buff.ResearchTimeToComplete.Hours, buff.ResearchTimeToComplete.Minutes, field.value);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            buff.ResearchTimeToComplete = new System.TimeSpan(buff.ResearchTimeToComplete.Days, buff.ResearchTimeToComplete.Hours, buff.ResearchTimeToComplete.Minutes, field.value);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer4.Add(up);
                        dateContainer4.Add(down);
                        container.Add(dateContainer4);
                        break;
                    }
                    default: {
                        break;
                    }
                }
                panel.Add(container);

            }


            
            
    }

    

    public static void MakeResearchDurationSelector(Research research, VisualElement panel)
        {
            var container = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.FlexStart, marginRight = 4 } };
            List<string> dates = new List<string>();
            dates.Add("Day");
            dates.Add("Hour");
            dates.Add("Minutes");
            dates.Add("Seconds");


            foreach (string date in dates) {

                switch (date) {
                    case "Day": {
                        var dateContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Days");
                        dateContainer.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer.Add(field);
                        field.value = research.ResearchTimeToComplete.Days;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Max(0, field.value);
                            research.ResearchTimeToComplete = new System.TimeSpan(field.value, research.ResearchTimeToComplete.Hours, research.ResearchTimeToComplete.Minutes, research.ResearchTimeToComplete.Seconds);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Max(0, field.value);
                            research.ResearchTimeToComplete = new System.TimeSpan(field.value, research.ResearchTimeToComplete.Hours, research.ResearchTimeToComplete.Minutes, research.ResearchTimeToComplete.Seconds);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer.Add(up);
                        dateContainer.Add(down);
                        container.Add(dateContainer);
                        break;
                    }
                    case "Hour": {
                        var dateContainer2 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Hours");
                        dateContainer2.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer2.Add(field);

                        field.value = research.ResearchTimeToComplete.Hours;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Clamp(field.value, 0, 23);
                            research.ResearchTimeToComplete = new System.TimeSpan(research.ResearchTimeToComplete.Days, field.value, research.ResearchTimeToComplete.Minutes, research.ResearchTimeToComplete.Seconds);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Clamp(field.value, 0, 23);
                            research.ResearchTimeToComplete = new System.TimeSpan(research.ResearchTimeToComplete.Days, field.value, research.ResearchTimeToComplete.Minutes, research.ResearchTimeToComplete.Seconds);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer2.Add(up);
                        dateContainer2.Add(down);
                        container.Add(dateContainer2);
                        break;
                    }
                    case "Minutes": {
                        var dateContainer3 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Minutes");
                        dateContainer3.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer3.Add(field);

                        field.value = research.ResearchTimeToComplete.Minutes;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            research.ResearchTimeToComplete = new System.TimeSpan(research.ResearchTimeToComplete.Days, research.ResearchTimeToComplete.Hours, field.value, research.ResearchTimeToComplete.Seconds);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            research.ResearchTimeToComplete = new System.TimeSpan(research.ResearchTimeToComplete.Days, research.ResearchTimeToComplete.Hours, field.value, research.ResearchTimeToComplete.Seconds);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer3.Add(up);
                        dateContainer3.Add(down);
                        container.Add(dateContainer3);
                        break;
                    }
                    case "Seconds": {
                        var dateContainer4 = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginRight = 4 } };
                        Label DateLabel = new Label("Seconds");
                        dateContainer4.Add(DateLabel);
                        IntegerField field = new IntegerField() { value = 0 };
                        dateContainer4.Add(field);

                        field.value = research.ResearchTimeToComplete.Seconds;

                        var up   = new Button(() => {
                            field.value++;
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            research.ResearchTimeToComplete = new System.TimeSpan(research.ResearchTimeToComplete.Days, research.ResearchTimeToComplete.Hours, research.ResearchTimeToComplete.Minutes, field.value);
                            }) { text = "▲" };
                        var down = new Button(() => { 
                            field.value--; 
                            field.value = Mathf.Clamp(field.value, 0, 59);
                            research.ResearchTimeToComplete = new System.TimeSpan(research.ResearchTimeToComplete.Days, research.ResearchTimeToComplete.Hours, research.ResearchTimeToComplete.Minutes, field.value);
                            }) { text = "▼" };
                        up.style.width = down.style.width = 22;

                        dateContainer4.Add(up);
                        dateContainer4.Add(down);
                        container.Add(dateContainer4);
                        break;
                    }
                    default: {
                        break;
                    }
                }
                panel.Add(container);

            }


            
            
        }

    private static void renderResearchList(bool isBuffAdd, Research buffAddResearch) {
        if (ResearchsFoldout == null) {
            return;
        }

        if (ResearchsScrollView != null) {
            ResearchsFoldout.Remove(ResearchsScrollView);
        }
        ResearchsScrollView = new ScrollView();

        foldouts.Clear();

        if (GameConfig.researches == null || GameConfig.researches.Count == 0) {
            ResearchsScrollView.Add(new Label("No researches defined. Create one above to populate this section."));
            ResearchsFoldout.Add(ResearchsScrollView);
            return;
        }

        var instructions = new Label("Add four unique buffs to every research for it to be usable in game. Avoid reusing buffs across researches so players unlock each buff only once.");
        instructions.style.whiteSpace = WhiteSpace.Normal;
        instructions.style.marginBottom = 6;
        ResearchsScrollView.Add(instructions);

        var typeOrder = new List<Research.ResearchTypeEnum>
        {
            Research.ResearchTypeEnum.Research,
            Research.ResearchTypeEnum.Base,
            Research.ResearchTypeEnum.Military,
            Research.ResearchTypeEnum.Commander
        };

        List<Research> researches = GameConfig.researches
            .OrderBy(b => b.ResearchLevel)
            .ThenBy(b => b.ResearchName)
            .ToList();

        foreach (var researchType in typeOrder) {

            Foldout typeFoldout = new Foldout();
            typeFoldout.text = researchType.ToString();
            typeFoldout.value = researchTypeFoldoutState.TryGetValue(researchType, out var savedTypeState) ? savedTypeState : true;
            typeFoldout.RegisterValueChangedCallback(evt =>
            {
                researchTypeFoldoutState[researchType] = evt.newValue;
            });
            ResearchsScrollView.Add(typeFoldout);

            var researchesOfType = researches.Where(r => r.ResearchType == researchType).ToList();
            if (researchesOfType.Count == 0) {
                typeFoldout.Add(new Label("No researches defined for this type yet."));
                continue;
            }

            foreach (Research research in researchesOfType) {

                EnsureResearchSlots(research);

                string researchKey = !string.IsNullOrEmpty(research.Id) ? research.Id : research.ResearchName;

                Foldout ResearchsNameFoldout = new Foldout();
                ResearchsNameFoldout.text = research.ResearchName;
                ResearchsNameFoldout.value = researchFoldoutState.TryGetValue(researchKey, out var savedState)
                    ? savedState
                    : buffAddResearch.ResearchName == research.ResearchName;
                typeFoldout.Add(ResearchsNameFoldout);
                foldouts.Add(ResearchsNameFoldout);
                ResearchsNameFoldout.RegisterValueChangedCallback(evt =>
                {
                    researchFoldoutState[researchKey] = evt.newValue;
                    if (evt.newValue)
                    {
                        foreach (var other in foldouts)
                        {
                            if (other != ResearchsNameFoldout)
                                other.value = false;
                        }
                    }
                });

            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.marginTop = 4;
            separator.style.marginBottom = 4;
            separator.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
            ResearchsNameFoldout.Add(separator);

            Label LevelUpLabel = new Label("Add the research Level Up duration.");
            ResearchsNameFoldout.Add(LevelUpLabel);

            MakeResearchDurationSelector(research, ResearchsNameFoldout);


            var BuffCompletedSprite = new ObjectField("Buff Completed Sprite")
                {
                    objectType = typeof(Sprite),
                    allowSceneObjects = false
                };

                BuffCompletedSprite.RegisterValueChangedCallback(e =>
                {
                    var spr = e.newValue as Sprite;
                    if (!TryGetSpriteResourcesPath(spr, out var resPath, out var subName))
                    {
                        Debug.LogError("Sprite must live under a 'Resources' folder.");
                        return;
                    }

                    research.BuffCompletedSpritePath = resPath;
                    renderResearchList(true, research);
                });
            ResearchsNameFoldout.Add(BuffCompletedSprite);

            IntegerField ResearchLevelEdit = new IntegerField();
            ResearchLevelEdit.label = "Research Level";
            ResearchLevelEdit.value = research.ResearchLevel;
            ResearchLevelEdit.RegisterValueChangedCallback((ChangeEvent<int> evt) =>
            {
                research.ResearchLevel = evt.newValue; 
            });
            ResearchsNameFoldout.Add(ResearchLevelEdit);

            var researchTypeField = new EnumField("Research Type", research.ResearchType);
            researchTypeField.RegisterValueChangedCallback(evt =>
            {
                research.ResearchType = (Research.ResearchTypeEnum)evt.newValue;
            });
            ResearchsNameFoldout.Add(researchTypeField);

            renderReseachBuffDropdown(research, ResearchsNameFoldout);

            for (int i = 0; i < research.BuffList.Length; i++) {
            Buff buff = research.BuffList[i];
            if(buff != null) {
            Box box = new Box();
            box.style.height = 50;
            box.style.display = DisplayStyle.Flex;
            box.style.flexDirection = FlexDirection.Row;
            box.style.justifyContent = Justify.SpaceBetween;
            box.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            box.style.borderTopColor = new Color(0f, 0f, 0f, 1f);
            box.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            ResearchsNameFoldout.Add(box);

            Box TextBox = new Box();
            TextBox.style.height = 50;
            TextBox.style.width = 200;
            TextBox.style.display = DisplayStyle.Flex;
            TextBox.style.flexDirection = FlexDirection.Row;
            TextBox.style.justifyContent = Justify.FlexStart;
            TextBox.style.alignItems = Align.Center;
            TextBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            TextBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(TextBox);

            Sprite sprite = LoadSpriteFromResources(buff.SpritePath);
            var preview = new VisualElement
            {
                style =
                {
                    width = 50,
                    height = 50,
                    backgroundImage = new StyleBackground(sprite),
                    display = DisplayStyle.Flex
                }
            };
            TextBox.Add(preview);

            Label MapID = new Label(buff.BuffName);
            MapID.style.display = DisplayStyle.Flex;
            MapID.style.flexDirection = FlexDirection.Column;
            MapID.style.justifyContent = Justify.Center;
            MapID.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(MapID);


            Box ButtonBox = new Box();
            ButtonBox.style.height = 50;
            ButtonBox.style.width = 100;
            ButtonBox.style.display = DisplayStyle.Flex;
            ButtonBox.style.flexDirection = FlexDirection.Column;
            ButtonBox.style.justifyContent = Justify.Center;
            ButtonBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            ButtonBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(ButtonBox);

            Button button5 = new Button();
            button5.text = "Remove";
            button5.style.display = DisplayStyle.Flex;
            int capturedIndex = i;
            button5.clicked += () => {
                RemoveBuffFromResearch(research, capturedIndex);
            };
            ButtonBox.Add(button5);
            }
            }
            Button button4 = new Button();
            button4.text = "Save";
            button4.clicked += research.UpdateResearch;
            ResearchsNameFoldout.Add(button4);
            Button deleteResearchButton = new Button();
            deleteResearchButton.text = "Delete Research";
            deleteResearchButton.clicked += () => {
                research.DeleteResearch();
                GameConfig.getResearches(() => { renderResearchList(false, new Research()); });
            };
            ResearchsNameFoldout.Add(deleteResearchButton);
            }
         }
         ResearchsFoldout.Add(ResearchsScrollView);
    }

      private static void renderBuffList() {
        if (BuffsFoldout == null) {
            return;
        }

        if (BuffsScrollView != null) {
            BuffsFoldout.Remove(BuffsScrollView);
        }
        BuffsScrollView = new ScrollView();

        if (GameConfig.buffs == null || GameConfig.buffs.Count == 0) {
            BuffsScrollView.Add(new Label("No buffs available. Save a buff to populate this list."));
            BuffsFoldout.Add(BuffsScrollView);
            return;
        }

         foreach (Buff buff in GameConfig.buffs) {

            Box box = new Box();
            box.style.height = 50;
            box.style.display = DisplayStyle.Flex;
            box.style.flexDirection = FlexDirection.Row;
            box.style.justifyContent = Justify.SpaceBetween;
            box.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            box.style.borderTopColor = new Color(0f, 0f, 0f, 1f);
            box.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            BuffsScrollView.Add(box);

            Box TextBox = new Box();
            TextBox.style.height = 50;
            TextBox.style.width = 200;
            TextBox.style.display = DisplayStyle.Flex;
            TextBox.style.flexDirection = FlexDirection.Row;
            TextBox.style.justifyContent = Justify.FlexStart;
            TextBox.style.alignItems = Align.Center;
            TextBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            TextBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(TextBox);

            Sprite sprite = LoadSpriteFromResources(buff.SpritePath);
            var preview = new VisualElement
            {
                style =
                {
                    width = 50,
                    height = 50,
                    backgroundImage = new StyleBackground(sprite),
                    display = DisplayStyle.Flex
                }
            };
            TextBox.Add(preview);

            Label MapID = new Label(buff.BuffName);
            MapID.style.display = DisplayStyle.Flex;
            MapID.style.flexDirection = FlexDirection.Column;
            MapID.style.justifyContent = Justify.Center;
            MapID.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(MapID);


            box.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button != (int)MouseButton.LeftMouse) {
                    return;
                }

                if (evt.target is VisualElement ve && (ve is Button || ve.GetFirstAncestorOfType<Button>() != null)) {
                    return;
                }

                ShowToolkitModal(buff);
            });

            Box ButtonBox = new Box();
            ButtonBox.style.height = 50;
            ButtonBox.style.width = 100;
            ButtonBox.style.display = DisplayStyle.Flex;
            ButtonBox.style.flexDirection = FlexDirection.Column;
            ButtonBox.style.justifyContent = Justify.Center;
            ButtonBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            ButtonBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(ButtonBox);

            Button deleteButton = new Button();
            deleteButton.text = "Delete";
            deleteButton.style.display = DisplayStyle.Flex;
            deleteButton.clicked += delegate {buff.DeleteBuff(); };
            deleteButton.RegisterCallback<MouseUpEvent>(evt => evt.StopPropagation());
            ButtonBox.Add(deleteButton);
         }
         BuffsFoldout.Add(BuffsScrollView);
         if (ResearchsFoldout != null)
         {
            renderResearchList(false, new Research());
         }
    }

    public static void ShowToolkitModal(object Type, bool isNew = false)
    {
        switch (Type)
        {
            case Game.Attribute attribute: {
                var veil = new VisualElement { name = "attribute-modal-veil" };
                veil.style.position = Position.Absolute;
                veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
                veil.style.backgroundColor = new Color(0,0,0,0.35f);
                veil.pickingMode = PickingMode.Position;

                panel = new VisualElement { name = "attribute-modal-panel" };
                panel.style.alignSelf = Align.Center;
                panel.style.width = 420;
                panel.style.minHeight = 320;
                panel.style.marginLeft = panel.style.marginRight = 20;
                panel.style.marginTop = 40;
                panel.style.paddingLeft = 12;
                panel.style.paddingTop = 12;
                panel.style.paddingRight = 12;
                panel.style.paddingBottom = 12;
                panel.style.backgroundColor = new Color(0.18f,0.18f,0.18f,1f);
                panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius =
                    panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 6;

                var headerRow = new VisualElement
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        justifyContent = Justify.SpaceBetween,
                        marginBottom = 12
                    }
                };

                var cancelButton = new Button(() => ViElement.Remove(veil)) { text = "Cancel" };
                var saveButton = new Button(() => {
                    ViElement.Remove(veil);
                    if (isNew) {
                        attribute.SaveAttribute();
                    } else {
                        attribute.UpdateAttribute();
                    }
                    GameConfig.getAttributes(() => {
                        renderAtributesList();
                        renderBuffList();
                    });
                }) { text = isNew ? "Create Attribute" : "Save Attribute" };

                headerRow.Add(cancelButton);
                headerRow.Add(saveButton);
                panel.Add(headerRow);

                var nameField = new TextField("Attribute Name") { value = attribute.AttributeName };
                nameField.RegisterValueChangedCallback(evt => attribute.AttributeName = evt.newValue);
                panel.Add(nameField);

                var keyField = new TextField("Attribute Key") { value = attribute.AttributeKey };
                keyField.RegisterValueChangedCallback(evt => attribute.AttributeKey = evt.newValue);
                panel.Add(keyField);

                var valueField = new FloatField("Attribute Value") { value = attribute.AttributeFloat };
                valueField.RegisterValueChangedCallback(evt => attribute.AttributeFloat = evt.newValue);
                panel.Add(valueField);

                var typeField = new EnumField("Attribute Type", attribute.AttributeType);
                typeField.RegisterValueChangedCallback(evt =>
                {
                    attribute.AttributeType = (Game.Attribute.AttributeTypeEnum)evt.newValue;
                });
                panel.Add(typeField);

                var effectField = new FloatField("Effect Value") { value = attribute.EffectValue };
                effectField.RegisterValueChangedCallback(evt => attribute.EffectValue = evt.newValue);
                panel.Add(effectField);

                veil.Add(panel);
                ViElement.Add(veil);
                break;
            };
            case Buff buff: {
                var veil = new VisualElement { name = "modal-veil" };
                veil.style.position = Position.Absolute;
                veil.style.left = 0; veil.style.right = 0; veil.style.top = 0; veil.style.bottom = 0;
                veil.style.backgroundColor = new Color(0,0,0,0.35f);
                veil.pickingMode = PickingMode.Position; // intercept clicks (blocks underlying UI)

                panel = new VisualElement { name = "modal-panel" };
                panel.style.alignSelf = Align.Center; 
                panel.style.width = 500; 
                panel.style.minHeight = 600;
                panel.style.marginLeft = panel.style.marginRight = 20;
                panel.style.marginTop = 40; 
                panel.style.paddingLeft = 12; 
                panel.style.paddingTop = 12;
                panel.style.backgroundColor = new Color(0.18f,0.18f,0.18f,1);
                panel.style.borderTopLeftRadius = panel.style.borderTopRightRadius =
                panel.style.borderBottomLeftRadius = panel.style.borderBottomRightRadius = 6;

                var btns = new VisualElement { 
                    style = { 
                    justifyContent = Justify.SpaceBetween,
                    flexDirection = FlexDirection.Row 
                } };

                var Cancel = new Button(() => ViElement.Remove(veil)) { text = "Cancel" };
                var Save = new Button(() => {
                    ViElement.Remove(veil);
                    if (isNew) {
                        buff.SaveBuff();
                    } else {
                        buff.UpdateBuff();
                    }
                    GameConfig.getBuffs(renderBuffList);
                    GameConfig.getResearches(() => { renderResearchList(false, new Research()); });
                    }) { text = isNew ? "Create Buff" : "Save Buff" };
                btns.Add(Cancel);
                btns.Add(Save);

                panel.Add(btns);

            Sprite sprite = LoadSpriteFromResources(buff.SpritePath);
                var preview = new VisualElement
                {
                    style =
                    {
                        width = 100,
                        height = 100,
                        backgroundImage = new StyleBackground(sprite)
                    }
                };
                panel.Add(preview);

                TextField BuffName = new TextField();
                BuffName.label = "Name the Buff";
                BuffName.value = buff.BuffName;
                BuffName.RegisterValueChangedCallback((ChangeEvent<string> evt) =>
                {
                    buff.BuffName = evt.newValue; 
                });
                panel.Add(BuffName);

                Label BuffDescriptionLabel = new Label("Buff Description");
                panel.Add(BuffDescriptionLabel);

                TextField BuffDescription = new TextField();
                BuffDescription.value = buff.BuffDescription;
                BuffDescription.multiline = true;
                BuffDescription.RegisterValueChangedCallback((ChangeEvent<string> evt) =>
                {
                    buff.BuffDescription = evt.newValue; 
                });
                panel.Add(BuffDescription);

                var spriteField = new ObjectField("Sprite")
                {
                    objectType = typeof(Sprite),
                    allowSceneObjects = false
                };

                spriteField.RegisterValueChangedCallback(e =>
                {
                    var spr = e.newValue as Sprite;
                    if (!TryGetSpriteResourcesPath(spr, out var resPath, out var subName))
                    {
                        Debug.LogError("Sprite must live under a 'Resources' folder.");
                        return;
                    }

                    buff.SpritePath = resPath;
                });
                panel.Add(spriteField);

                MakeBuffDurationSelector(buff, panel);

                renderBuffAtributesDropdown(buff, panel);

                BuffsAttributesScrollView = new ScrollView();
                panel.Add(BuffsAttributesScrollView);
                renderBuffAtributesList(buff, panel);

                veil.Add(panel);
                ViElement.Add(veil);
                break;
            };
            default: throw new ArgumentException("Unsupported type");
        }
    }

    private static void renderBuffAtributesList(Buff buff, VisualElement panel) {
        if (panel == null) {
            return;
        }

        if (BuffsAttributesScrollView != null) {
            panel.Remove(BuffsAttributesScrollView);
        }
        BuffsAttributesScrollView = new ScrollView();

        if (buff.attributeList == null || buff.attributeList.Count == 0) {
            BuffsAttributesScrollView.Add(new Label("No attributes linked to this buff yet."));
            panel.Add(BuffsAttributesScrollView);
            return;
        }

         foreach (Game.Attribute attribute in buff.attributeList) {

            attribute.AttributeType = Game.Attribute.AttributeTypeEnum.Buff;

            Box box = new Box();
            box.style.height = 50;
            box.style.display = DisplayStyle.Flex;
            box.style.flexDirection = FlexDirection.Row;
            box.style.justifyContent = Justify.SpaceBetween;
            box.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            box.style.borderTopColor = new Color(0f, 0f, 0f, 1f);
            box.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            BuffsAttributesScrollView.Add(box);

            Box TextBox = new Box();
            TextBox.style.height = 50;
            TextBox.style.width = 150;
            TextBox.style.display = DisplayStyle.Flex;
            TextBox.style.flexDirection = FlexDirection.Column;
            TextBox.style.justifyContent = Justify.Center;
            TextBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            TextBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(TextBox);

            Box EnumBox = new Box();
            EnumBox.style.height = 50;
            EnumBox.style.width = 200;
            EnumBox.style.display = DisplayStyle.Flex;
            EnumBox.style.flexDirection = FlexDirection.Column;
            EnumBox.style.justifyContent = Justify.FlexStart;
            EnumBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            EnumBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(EnumBox);
            

            string[] names = System.Enum.GetNames(typeof(Game.Attribute.AttributeTypeEnum)); 
            var popup = new PopupField<string>(
            choices: new System.Collections.Generic.List<string>(names),  
            defaultValue: names[0], 
            formatSelectedValueCallback: a => {
                attribute.AttributeType = (Game.Attribute.AttributeTypeEnum)System.Enum.Parse(typeof(Game.Attribute.AttributeTypeEnum), a);
                return a;
            }
            
            );
            EnumBox.Add(popup);

            FloatField EffectValue = new FloatField();
            EffectValue.label = "Effect Value";
            EffectValue.value = attribute.EffectValue;
            EffectValue.RegisterValueChangedCallback((ChangeEvent<float> evt) =>
            {
                attribute.EffectValue = evt.newValue; 
            });
            EnumBox.Add(EffectValue);

            Label MapID = new Label(attribute.AttributeName);
            MapID.style.display = DisplayStyle.Flex;
            MapID.style.flexDirection = FlexDirection.Column;
            MapID.style.justifyContent = Justify.Center;
            MapID.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(MapID);

            Label TitleKey = new Label(attribute.AttributeKey);
            TitleKey.style.display = DisplayStyle.Flex;
            TitleKey.style.flexDirection = FlexDirection.Column;
            TitleKey.style.justifyContent = Justify.Center;
            TitleKey.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(TitleKey);

            Label MapSize = new Label(attribute.AttributeFloat.ToString());
            MapSize.style.display = DisplayStyle.Flex;
            MapSize.style.flexDirection = FlexDirection.Column;
            MapSize.style.justifyContent = Justify.Center;
            MapSize.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(MapSize);

            Box ButtonBox = new Box();
            ButtonBox.style.height = 50;
            ButtonBox.style.width = 100;
            ButtonBox.style.display = DisplayStyle.Flex;
            ButtonBox.style.flexDirection = FlexDirection.Column;
            ButtonBox.style.justifyContent = Justify.Center;
            ButtonBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            ButtonBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(ButtonBox);

            Button button3 = new Button();
            button3.text = "Delete";
            button3.style.display = DisplayStyle.Flex;
            button3.clicked += () => {
                buff.attributeList.Remove(attribute);
                renderBuffAtributesList(buff, panel);
            };
            ButtonBox.Add(button3);
         }
         panel.Add(BuffsAttributesScrollView);
    }

    private static void renderReseachBuffDropdown(Research research, VisualElement panel) {
        if (panel == null) {
            return;
        }

        if (GameConfig.buffs == null || GameConfig.buffs.Count == 0) {
            panel.Add(new Label("No buffs available. Create a buff before assigning it to a research."));
            return;
        }

        Buff SelectedBuff = GameConfig.buffs[0];
        Box AttributeRow = new Box();
        AttributeRow.style.height = 25;
        AttributeRow.style.display = DisplayStyle.Flex;
        AttributeRow.style.flexDirection = FlexDirection.Row;
        AttributeRow.style.justifyContent = Justify.FlexStart;
        AttributeRow.style.alignItems = Align.Center;
        AttributeRow.style.marginBottom = 4;
        panel.Add(AttributeRow);

        var popup = new PopupField<Buff>(
            label: "Pick a Buff",
            choices: GameConfig.buffs,  
            defaultValue: SelectedBuff, 
            formatSelectedValueCallback: a => {
                SelectedBuff = a;
                return a.BuffName;
            },
            formatListItemCallback: a => $"{a.BuffName}"
        );
        popup.style.display = DisplayStyle.Flex;
        popup.style.flexGrow = 1;
        popup.style.minWidth = 180;
        AttributeRow.Add(popup);

        Button button4 = new Button();
        button4.text = "Add";
        button4.style.marginLeft = 6;
        button4.style.flexShrink = 0;
        button4.clicked += () => {
            AddBuffToResearch(research, SelectedBuff);
        };
        AttributeRow.Add(button4);
    }

    private static void renderBuffAtributesDropdown(Buff buff, VisualElement panel) {
        if (panel == null) {
            return;
        }

        if (GameConfig.attributes == null || GameConfig.attributes.Count == 0) {
            panel.Add(new Label("No attributes available. Create an attribute before adding it to a buff."));
            return;
        }

        Game.Attribute SelectedAttribute = GameConfig.attributes[0];
        Box AttributeRow = new Box();
            AttributeRow.style.height = 25;
            AttributeRow.style.display = DisplayStyle.Flex;
            AttributeRow.style.flexDirection = FlexDirection.Row;
            AttributeRow.style.justifyContent = Justify.FlexStart;
            AttributeRow.style.alignItems = Align.Center;
            AttributeRow.style.marginBottom = 4;
            panel.Add(AttributeRow);
        var popup = new PopupField<Game.Attribute>(
            label: "Pick Attribute",
            choices: GameConfig.attributes,  
            defaultValue: SelectedAttribute, 
            formatSelectedValueCallback: a => {
                SelectedAttribute = a;
                return a.AttributeName;
            },
            formatListItemCallback: a => $"{a.AttributeName} ({a.AttributeKey})"
        );
        popup.style.display = DisplayStyle.Flex;
        popup.style.flexGrow = 1;
        popup.style.minWidth = 180;
        AttributeRow.Add(popup);

        Button button4 = new Button();
        button4.text = "Add";
        button4.style.marginLeft = 6;
        button4.style.flexShrink = 0;
        button4.clicked += () => { 
            buff.attributeList.Add(SelectedAttribute);
            renderBuffAtributesList(buff, panel);
        };
        AttributeRow.Add(button4);
    }

    private static void AddBuffToResearch(Research research, Buff buff)
    {
        if (research == null || buff == null) {
            Debug.LogWarning("Research or buff is null; cannot add.");
            return;
        }

        EnsureResearchSlots(research);

        for (int i = 0; i < research.BuffList.Length; i++) {
            if (research.BuffList[i] != null && research.BuffList[i].Id == buff.Id) {
                Debug.LogWarning($"Buff '{buff.BuffName}' is already assigned to this research.");
                researchFoldoutState[GetResearchKey(research)] = true;
                researchTypeFoldoutState[research.ResearchType] = true;
                renderResearchList(false, research);
                return;
            }
        }

        for (int i = 0; i < research.BuffList.Length; i++) {
            if (research.BuffList[i] == null) {
                research.BuffList[i] = buff;
                researchFoldoutState[GetResearchKey(research)] = true;
                researchTypeFoldoutState[research.ResearchType] = true;
                renderResearchList(false, research);
                return;
            }
        }

        Debug.LogWarning("All buff slots are filled for this research.");
    }

    private static void RemoveBuffFromResearch(Research research, int index)
    {
        if (research == null) {
            return;
        }

        EnsureResearchSlots(research);

        if (index < 0 || index >= research.BuffList.Length) {
            return;
        }

        research.BuffList[index] = null;
        researchFoldoutState[GetResearchKey(research)] = true;
        researchTypeFoldoutState[research.ResearchType] = true;
        renderResearchList(false, research);
    }

    private static void EnsureResearchSlots(Research research)
    {
        if (research.BuffList == null || research.BuffList.Length != 4) {
            var newArray = new Buff[4];
            if (research.BuffList != null) {
                for (int i = 0; i < Mathf.Min(research.BuffList.Length, newArray.Length); i++) {
                    newArray[i] = research.BuffList[i];
                }
            }
            research.BuffList = newArray;
        }
    }

    private static string GetResearchKey(Research research)
    {
        if (research == null) {
            return string.Empty;
        }

        if (!string.IsNullOrEmpty(research.Id)) {
            return research.Id;
        }

        return string.IsNullOrEmpty(research.ResearchName) ? research.GetHashCode().ToString() : research.ResearchName;
    }

    private static async void RunMigrationAsync()
    {
        if (isMigrating)
        {
            return;
        }

        isMigrating = true;
        migrateDbButton?.SetEnabled(false);

        try
        {
            await FirestoreMigration.RunAsync();
            EditorUtility.DisplayDialog("Firestore Migration", "Database migration completed successfully.", "OK");
            hasInitialDataLoad = false;
            TryRequestGameConfigData();
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            EditorUtility.DisplayDialog("Firestore Migration", "Migration failed. Check the Console for details.", "OK");
        }
        finally
        {
            isMigrating = false;
            UpdateFirebaseStatus();
        }
    }


    private static void renderAtributesList() {
        if (AttributeFoldout == null) {
            return;
        }

        if (AttributesScrollView != null) {
            AttributeFoldout.Remove(AttributesScrollView);
        }

        AttributesScrollView = new ScrollView();

        if (GameConfig.attributes == null || GameConfig.attributes.Count == 0) {
            AttributesScrollView.Add(new Label("No attributes found. Create one above to get started."));
            AttributeFoldout.Add(AttributesScrollView);
            return;
        }

        foreach (Game.Attribute attribute in GameConfig.attributes) {
            Debug.Log(attribute.AttributeName);

            Box box = new Box();
            box.style.height = 50;
            box.style.display = DisplayStyle.Flex;
            box.style.flexDirection = FlexDirection.Row;
            box.style.justifyContent = Justify.SpaceBetween;
            box.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            box.style.borderTopColor = new Color(0f, 0f, 0f, 1f);
            box.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            AttributesScrollView.Add(box);

            Box TextBox = new Box();
            TextBox.style.height = 50;
            TextBox.style.width = 200;
            TextBox.style.display = DisplayStyle.Flex;
            TextBox.style.flexDirection = FlexDirection.Column;
            TextBox.style.justifyContent = Justify.Center;
            TextBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            TextBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(TextBox);

            Label MapID = new Label(attribute.AttributeName);
            MapID.style.display = DisplayStyle.Flex;
            MapID.style.flexDirection = FlexDirection.Column;
            MapID.style.justifyContent = Justify.Center;
            MapID.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(MapID);

            Label TitleKey = new Label(attribute.AttributeKey);
            TitleKey.style.display = DisplayStyle.Flex;
            TitleKey.style.flexDirection = FlexDirection.Column;
            TitleKey.style.justifyContent = Justify.Center;
            TitleKey.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(TitleKey);

            Label MapSize = new Label(attribute.AttributeFloat.ToString());
            MapSize.style.display = DisplayStyle.Flex;
            MapSize.style.flexDirection = FlexDirection.Column;
            MapSize.style.justifyContent = Justify.Center;
            MapSize.style.color = new Color(0f, 0f, 0f, 1f);
            TextBox.Add(MapSize);

            box.RegisterCallback<MouseUpEvent>(evt => {
                if (evt.button != (int)MouseButton.LeftMouse) {
                    return;
                }

                if (evt.target is VisualElement ve && (ve is Button || ve.GetFirstAncestorOfType<Button>() != null)) {
                    return;
                }

                ShowToolkitModal(attribute);
            });

            Box ButtonBox = new Box();
            ButtonBox.style.height = 50;
            ButtonBox.style.width = 100;
            ButtonBox.style.display = DisplayStyle.Flex;
            ButtonBox.style.flexDirection = FlexDirection.Column;
            ButtonBox.style.justifyContent = Justify.Center;
            ButtonBox.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            ButtonBox.style.borderBottomColor = new Color(0f, 0f, 0f, 1f);
            box.Add(ButtonBox);

            Button button3 = new Button();
            button3.text = "Delete";
            button3.style.display = DisplayStyle.Flex;
            button3.clicked += delegate {deleteAttribute(attribute); };
            button3.RegisterCallback<MouseUpEvent>(evt => evt.StopPropagation());
            ButtonBox.Add(button3);
         }
         AttributeFoldout.Add(AttributesScrollView);
    }

   public static async void deleteAttribute(Game.Attribute attribute) {
        attribute.DeleteAttribute();
        GameConfig.getAttributes(renderAtributesList);
   }

   public static bool TryGetSpriteResourcesPath(Sprite sprite, out string pathNoExt, out string subSpriteName)
    {
        pathNoExt = null;
        subSpriteName = null;
        if (sprite == null) return false;

        // e.g. "Assets/Art/Resources/UI/Icons/Swords.png"
        string assetPath = AssetDatabase.GetAssetPath(sprite);
        if (string.IsNullOrEmpty(assetPath)) return false;

        assetPath = assetPath.Replace('\\', '/');
        int i = assetPath.LastIndexOf("/Resources/");
        if (i < 0) return false; // NOT inside a Resources folder

        // Trim to just after ".../Resources/"
        string rel = assetPath.Substring(i + "/Resources/".Length);
        // Remove extension: "UI/Icons/Swords"
        pathNoExt = Path.ChangeExtension(rel, null);

        // For multi-sprite textures, remember which sub-sprite
        subSpriteName = sprite.name;
        return true;
    }

    private static void TryRequestGameConfigData()
    {
        if (AttributeFoldout == null || BuffsFoldout == null || ResearchsFoldout == null) {
            return;
        }
        if (MapsFoldout == null) {
            return;
        }

        if (Auth.User.user == null) {
            hasInitialDataLoad = false;
            ShowAuthRequiredMessages();
            return;
        }

        DB.Default.Init();
        if (DB.Default.gameConfig == null) {
            hasInitialDataLoad = false;
            ShowAuthRequiredMessages();
            return;
        }

        if (hasInitialDataLoad) {
            return;
        }

        GameConfig.getAttributes(renderAtributesList);
        GameConfig.getBuffs(renderBuffList);
        GameConfig.getResearches(() => { renderResearchList(false, new Research()); });
        EnsureMapsListener();
        UpdateMapStatus(string.Empty, false);
        hasInitialDataLoad = true;
    }

    private static void ShowAuthRequiredMessages()
    {
        ShowMessage(ref AttributesScrollView, AttributeFoldout, "Sign in to load attributes.");
        ShowMessage(ref BuffsScrollView, BuffsFoldout, "Sign in to load buffs.");
        ShowMessage(ref ResearchsScrollView, ResearchsFoldout, "Sign in to load researches.");
        ShowMessage(ref MapsScrollView, MapsFoldout, "Sign in to manage maps.");
        UpdateMapStatus("Sign in to manage maps.", true);
        DisposeMapsListener();
    }

    private static void ShowMessage(ref ScrollView scrollView, Foldout foldout, string message)
    {
        if (foldout == null) {
            return;
        }

        if (scrollView != null) {
            foldout.Remove(scrollView);
        }

        scrollView = new ScrollView();
        scrollView.Add(new Label(message));
        foldout.Add(scrollView);
    }

    private static Transform FindChildWithFallback(Transform root, params string[] relativePaths)
    {
        if (root == null || relativePaths == null) {
            return null;
        }

        foreach (var path in relativePaths) {
            if (string.IsNullOrEmpty(path)) {
                continue;
            }

            Transform child = root.Find(path);
            if (child != null) {
                return child;
            }
        }

        return null;
    }

    private static void CreateMapFromUI()
    {
        if (mapNameField == null || mapSizeSlider == null)
        {
            return;
        }

        string mapName = mapNameField.value?.Trim();
        if (string.IsNullOrEmpty(mapName))
        {
            UpdateMapStatus("Enter a planet name before creating a map.", true);
            return;
        }

        if (Auth.User.user == null)
        {
            UpdateMapStatus("Sign in to manage maps.", true);
            return;
        }

        DB.Default.Init();
        if (DB.Default.maps == null)
        {
            UpdateMapStatus("Firestore is not ready. Try again in a moment.", true);
            return;
        }

        try
        {
            if (string.IsNullOrEmpty(mapPlanetSurfaceResourcePath))
            {
                UpdateMapStatus("Select a planet surface sprite located under Assets/Resources before creating a map.", true);
                return;
            }

            var map = new Map
            {
                PlanetSurface = mapPlanetSurfaceResourcePath
            };
            map.createMap(mapName, mapSizeSlider.value);
            UpdateMapStatus($"Creating map '{mapName}'...", false);
            mapNameField.value = string.Empty;
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            UpdateMapStatus("Failed to create map. Check the Console for details.", true);
        }
    }


    private static Sprite LoadSpriteFromResources(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath))
        {
            return null;
        }

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite != null)
        {
            return sprite;
        }

        // Fallback for legacy paths or when the folder was omitted.
        if (!resourcePath.StartsWith("MapPallet/Grass/", System.StringComparison.OrdinalIgnoreCase))
        {
            string candidate = resourcePath.StartsWith("MapPallet/", System.StringComparison.OrdinalIgnoreCase)
                ? resourcePath.Insert("MapPallet/".Length, "Grass/")
                : $"MapPallet/Grass/{resourcePath}";
            sprite = Resources.Load<Sprite>(candidate);
            if (sprite != null)
            {
                return sprite;
            }
        }

        Debug.LogWarning($"Unable to load sprite at Resources path '{resourcePath}'.");
        return null;
    }

    private static string GetResourcePathForSprite(Sprite sprite)
    {
        if (sprite == null)
        {
            return null;
        }

        string assetPath = AssetDatabase.GetAssetPath(sprite);
        const string resourcesPrefix = "Assets/Resources/";
        if (!assetPath.StartsWith(resourcesPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string relativePath = assetPath.Substring(resourcesPrefix.Length);
        int extensionIndex = relativePath.LastIndexOf('.');
        if (extensionIndex >= 0)
        {
            relativePath = relativePath.Substring(0, extensionIndex);
        }
        return relativePath;
    }

    private static void HandlePlanetSurfaceSpriteChanged(Sprite sprite)
    {
        string resourcePath = GetResourcePathForSprite(sprite);
        if (sprite != null && string.IsNullOrEmpty(resourcePath))
        {
            UpdateMapStatus("Selected sprite must be located under Assets/Resources.", true);
            if (mapPlanetSurfaceField != null)
            {
                mapPlanetSurfaceField.SetValueWithoutNotify(LoadSpriteFromResources(mapPlanetSurfaceResourcePath));
            }
            return;
        }

        mapPlanetSurfaceResourcePath = resourcePath ?? string.Empty;
    }

    private static void UpdateMapStatus(string message, bool isError)
    {
        if (mapStatusLabel == null)
        {
            return;
        }

        mapStatusLabel.text = message;
        mapStatusLabel.style.color = isError
            ? new Color(0.9f, 0.3f, 0.3f, 1f)
            : new Color(0.7f, 0.7f, 0.7f, 1f);
    }

    private static void EnsureMapsListener()
    {
        if (mapsListener != null)
        {
            return;
        }

        if (DB.Default.maps == null)
        {
            return;
        }

        mapsListener = DB.Default.maps.Listen(snapshot =>
        {
            var captured = snapshot;
            EditorApplication.delayCall += () => UpdateMapList(captured);
        });
    }

    private static void DisposeMapsListener()
    {
        if (mapsListener == null)
        {
            return;
        }

        mapsListener.Stop();
        mapsListener = null;
    }

    private static void UpdateMapList(QuerySnapshot snapshot)
    {
        if (MapsScrollView == null)
        {
            return;
        }

        MapsScrollView.Clear();

        if (snapshot == null || snapshot.Documents == null || !snapshot.Documents.Any())
        {
            MapsScrollView.Add(new Label("No maps available yet."));
            return;
        }

        foreach (DocumentSnapshot documentSnapshot in snapshot.Documents)
        {
            Map map = documentSnapshot.ConvertTo<Map>();
            if (map == null)
            {
                continue;
            }
            map.Id = documentSnapshot.Id;
            MapsScrollView.Add(BuildMapRow(map));
        }
    }

    private static VisualElement BuildMapRow(Map map)
    {
        var box = new Box();
        box.style.height = 80;
        box.style.marginBottom = 6;
        box.style.display = DisplayStyle.Flex;
        box.style.flexDirection = FlexDirection.Row;
        box.style.justifyContent = Justify.SpaceBetween;
        box.style.backgroundColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        box.style.borderTopColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        box.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        box.style.paddingLeft = 10;
        box.style.paddingRight = 10;
        box.style.alignItems = Align.Center;

        var textColumn = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.Center,
                flexGrow = 1
            }
        };

        var idLabel = new Label($"Id: {map.Id}")
        {
            style = { color = new Color(0.8f, 0.8f, 0.8f, 1f) }
        };
        textColumn.Add(idLabel);

        string planetName = string.IsNullOrEmpty(map.PlanetName) ? "Unnamed" : map.PlanetName;
        var nameLabel = new Label($"Planet Name: {planetName}")
        {
            style = { color = Color.white }
        };
        textColumn.Add(nameLabel);

        var sizeLabel = new Label($"Tiles Wide: {map.PlanetSize}")
        {
            style = { color = new Color(0.8f, 0.8f, 0.8f, 1f) }
        };
        textColumn.Add(sizeLabel);

        box.Add(textColumn);

        var buttonColumn = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Column,
                justifyContent = Justify.Center,
                alignItems = Align.FlexEnd,
                width = 140
            }
        };

        var loadButton = new Button(map.LoadMap) { text = "Load Map" };
        loadButton.style.marginBottom = 2;
        var saveButton = new Button(map.SaveMap) { text = "Save Map" };
        saveButton.style.marginBottom = 2;
        var deleteButton = new Button(() =>
        {
            map.DeleteMap();
        }) { text = "Delete" };

        buttonColumn.Add(loadButton);
        buttonColumn.Add(saveButton);
        buttonColumn.Add(deleteButton);

        box.Add(buttonColumn);
        return box;
    }

    private static VisualElement CreateInstructionStep(string stepNumber, string prefixText, string linkText = null, string linkUrl = null, string suffixText = null)
    {
        var row = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems = Align.FlexStart,
                marginBottom = 4
            }
        };

        var numberLabel = new Label($"{stepNumber}. ")
        {
            style =
            {
                unityTextAlign = TextAnchor.UpperLeft,
                marginRight = 2,
                whiteSpace = WhiteSpace.NoWrap
            }
        };
        row.Add(numberLabel);

        var content = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexWrap = Wrap.Wrap,
                alignItems = Align.FlexStart,
                flexGrow = 1
            }
        };

        if (!string.IsNullOrEmpty(prefixText))
        {
            content.Add(new Label(prefixText) { style = { whiteSpace = WhiteSpace.Normal, marginRight = 2 } });
        }

        if (!string.IsNullOrEmpty(linkText) && !string.IsNullOrEmpty(linkUrl))
        {
            var linkElement = MakeHyperlink(linkText, linkUrl);
            linkElement.style.marginRight = 2;
            content.Add(linkElement);
        }

        if (!string.IsNullOrEmpty(suffixText))
        {
            content.Add(new Label(suffixText) { style = { whiteSpace = WhiteSpace.Normal } });
        }

        row.Add(content);
        return row;
    }

    private static Label MakeHyperlink(string text, string url)
    {
        var link = new Label
        {
            tooltip = url
        };
        link.enableRichText = true;
        link.text = $"<u>{text}</u>";
        link.style.color = new Color(0.3f, 0.6f, 1f, 1f);
        link.style.unityTextAlign = TextAnchor.MiddleLeft;
        link.style.whiteSpace = WhiteSpace.Normal;
        link.style.cursor = new StyleCursor(new UnityEngine.UIElements.Cursor());
        link.AddManipulator(new Clickable(() => Application.OpenURL(url)));
        return link;
    }

}
