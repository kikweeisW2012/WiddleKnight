using Modding;
using System.Collections.Generic;
using UnityEngine;
using static Satchel.EnemyUtils;
using static Satchel.GameObjectUtils;
using System.IO;

namespace WiddleKnight
{
    public class WiddleKnight : Mod, ICustomMenuMod
    {
        internal static WiddleKnight Instance;

        internal static List<GameObject> knights = new List<GameObject>();
        
        internal static Dictionary<ushort,GameObject> remoteKnights = new Dictionary<ushort,GameObject>();

        private static readonly Dictionary<string, tk2dSpriteAnimation> cachedLibraries = new Dictionary<string, tk2dSpriteAnimation>();

        public GlobalSettings GlobalSettings { get; set; } = new GlobalSettings();

        public void SaveSettings() => Log("Saving settings");

        public static bool HasPouch() => ModHooks.GetMod("HkmpPouch") is Mod;
        
        public override string GetVersion() => "pre-release 0.2.3.41";

        // Creating WiddleKnight
        public GameObject createKnightcompanion(GameObject followTarget = null)
        {
            // Clone the player
            var player = HeroController.instance.gameObject;
            player.SetActive(false);
            var knight = player.createCompanionFromPrefab();
            player.SetActive(true);
            
            // Remove components
            knight.RemoveComponent<HeroController>();
            knight.RemoveComponent<HeroAnimationController>();
            knight.RemoveComponent<HeroAudioController>();
            knight.RemoveComponent<ConveyorMovementHero>();

            // properties
            knight.name = "WiddleKnight";
            knight.GetAddComponent<MeshRenderer>().enabled = true;
            knight.GetAddComponent<Rigidbody2D>().gravityScale = 1f;

            // behavior
            var control = knight.GetAddComponent<WiddleKnightControl>();
            control.moveSpeed = 10.5f; // Max speed
            control.followDistance = 2f;
            control.IdleShuffleDistance = 0.01f;
            control.followTarget = followTarget;

            // collision
            var collider = knight.GetAddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f, 2.0f);
            collider.offset = new Vector2(0f, -0.4f);
            collider.enabled = true;

            // animations
            control.Animations.Add(State.Idle, "Idle");
            control.Animations.Add(State.laying, "Prostrate");
            control.Animations.Add(State.Walk, "Run");
            control.Animations.Add(State.Turn, "Map Walk");
            control.Animations.Add(State.Teleport, "Fall");
            control.Animations.Add(State.Jump, "Airborne");

            // Appling custom skins
            if (GlobalSettings.SelectedSkinOption == 2)
                ApplyCustomSkin(knight);

            // load
            Object.DontDestroyOnLoad(knight);

            knight.SetActive(true);
            return knight;
        }

        // Applies Custom Skin
        private void ApplyCustomSkin(GameObject knight)
        {
            try
            {
                if (knight == null) return;

                // Find skins folder
                string modPath = Path.GetDirectoryName(typeof(WiddleKnight).Assembly.Location);
                string skinsPath = Path.Combine(modPath, "Skins");
                
                if (!Directory.Exists(skinsPath)) return;

                string[] skinFolders = Directory.GetDirectories(skinsPath);
                if (skinFolders.Length == 0 || GlobalSettings.CustomSubOption >= skinFolders.Length) return;

                // Load texture
                string selectedSkinFolder = skinFolders[GlobalSettings.CustomSubOption];
                string knightPngPath = Path.Combine(selectedSkinFolder, "Knight.png");
                if (!File.Exists(knightPngPath)) return;

                byte[] textureBytes = File.ReadAllBytes(knightPngPath);
                Texture2D customTexture = new Texture2D(2, 2);
                customTexture.LoadImage(textureBytes);

                var spriteAnimator = knight.GetComponent<tk2dSpriteAnimator>();
                if (spriteAnimator?.Library == null) return;

                // Cash data
                if (!cachedLibraries.ContainsKey(selectedSkinFolder))
                {
                    Log($"Caching skin: {selectedSkinFolder}");

                    // Clone library to avoid modifying player's sprites
                    var newLibrary = Object.Instantiate(spriteAnimator.Library);
                    Object.DontDestroyOnLoad(newLibrary);

                    var materialMap = new Dictionary<Material, Material>();

                    // Clone skin
                    foreach (var clip in newLibrary.clips)
                    {
                        if (clip?.frames == null) continue;

                        foreach (var frame in clip.frames)
                        {
                            if (frame?.spriteCollection == null) continue;

                            var newCollection = Object.Instantiate(frame.spriteCollection);
                            Object.DontDestroyOnLoad(newCollection);
                            frame.spriteCollection = newCollection;
                            
                            var sprites = newCollection.spriteDefinitions;
                            if (sprites == null) continue;

                            for (int i = 0; i < sprites.Length; i++)
                            {
                                if (sprites[i]?.material == null) continue;

                                var originalMat = sprites[i].material;

                                if (!materialMap.ContainsKey(originalMat))
                                {
                                    var newMat = new Material(originalMat);
                                    newMat.mainTexture = customTexture;
                                    Object.DontDestroyOnLoad(newMat);
                                    Object.DontDestroyOnLoad(customTexture);
                                    materialMap[originalMat] = newMat;
                                }
                                
                                sprites[i].material = materialMap[originalMat];
                            }
                        }
                    }
                    
                    cachedLibraries[selectedSkinFolder] = newLibrary;
                }
                
                // Apply library
                spriteAnimator.Library = cachedLibraries[selectedSkinFolder];
                Log($"Applied skin: {Path.GetFileName(selectedSkinFolder)}");
            }
            catch (System.Exception e)
            {
                LogError($"Skin error: {e.Message}");
            }
        }

        // Initialize mod
        public override void Initialize()
        {
            Instance = this;
            Log($"Init - Option: {GlobalSettings.SelectedSkinOption}, Skin: {GlobalSettings.CustomSubOption}");
            
            ModHooks.HeroUpdateHook += update;
            ModHooks.AfterSavegameLoadHook += OnSaveGameLoad;
            
            if (HasPouch())
                PouchIntegration.Initialize();
        }
        
        // Clear knight
        private void OnSaveGameLoad(SaveGameData data)
        {
            Log($"Save loaded - Option: {GlobalSettings.SelectedSkinOption}, Skin: {GlobalSettings.CustomSubOption}");
            
            knights.RemoveAll(k => k == null);
            foreach (var knight in knights)
                if (knight != null) Object.Destroy(knight);
            knights.Clear();
        }

        // network
        public GameObject GetNetworkWiddleKnight(ushort id)
        {
            if (remoteKnights.TryGetValue(id, out var knight))
                return knight;
            
            remoteKnights[id] = createKnightcompanion();
            return remoteKnights[id];
        }
        
        // creates knight if needed
        public void update()
        {
            if (GlobalSettings.SelectedSkinOption == 0 || HeroController.instance == null)
                return;

            knights.RemoveAll(k => k == null);

            if (knights.Count < 1)
            {
                try
                {
                    knights.Add(createKnightcompanion());
                    Log("Knight created");
                }
                catch (System.Exception e)
                {
                    LogError($"Create failed: {e.Message}");
                }
            }
        }

        // Destroy WiddleKnight on settings change
        public void OnOptionChanged()
        {
            foreach (var knight in knights)
                if (knight != null) Object.Destroy(knight);
            knights.Clear();

            foreach (var kvp in remoteKnights)
                if (kvp.Value != null) Object.Destroy(kvp.Value);
            remoteKnights.Clear();
            
            cachedLibraries.Clear();
        }

        // Menu
        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates) =>
            WiddleKnightMenu.GetMenu(modListMenu);

        public bool ToggleButtonInsideMenu => false;
    }
}
