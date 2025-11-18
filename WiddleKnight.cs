using Modding;
using System.Collections.Generic;
using UnityEngine;
using static Satchel.EnemyUtils;
using static Satchel.GameObjectUtils;
using Satchel.BetterMenus;
using System.IO;

namespace WiddleKnight
{
    public class WiddleKnight : Mod, ICustomMenuMod
    {

        internal static WiddleKnight Instance;

        internal static List<GameObject> knights = new List<GameObject>();
        internal static Dictionary<ushort,GameObject> remoteKnights = new Dictionary<ushort,GameObject>();

        private static Dictionary<string, tk2dSpriteAnimation> cachedLibraries = new Dictionary<string, tk2dSpriteAnimation>();

        public GlobalSettings GlobalSettings { get; private set; } = new GlobalSettings();

        public static bool HasPouch()
        {
            var hasPouch = ModHooks.GetMod("HkmpPouch") is Mod;
            return hasPouch;
        }
        public override string GetVersion()
        {
            return "pre-release 0.2.3.34";
        }

        public GameObject createKnightcompanion(GameObject ft = null){
            HeroController.instance.gameObject.SetActive(false);
            var knight = HeroController.instance.gameObject.createCompanionFromPrefab();
            HeroController.instance.gameObject.SetActive(true);
            knight.RemoveComponent<HeroController>();
            knight.RemoveComponent<HeroAnimationController>();
            knight.RemoveComponent<HeroAudioController>();
            knight.RemoveComponent<ConveyorMovementHero>();

            knight.name = "WiddleKnight";
            knight.GetAddComponent<MeshRenderer>().enabled = true;
            knight.GetAddComponent<Rigidbody2D>().gravityScale = 1f;

            var kc = knight.GetAddComponent<WiddleKnightControl>();
            // needs to be max 11 or its glitchy
            kc.moveSpeed = 11f;
            kc.followDistance = 2f;
            kc.IdleShuffleDistance = 0.01f;

            if(ft != null){
                kc.followTarget = ft;
            }


            var collider = knight.GetAddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f,2.0f);
            collider.offset = new Vector2(0f,-0.4f);
            collider.enabled = true;

            kc.Animations.Add(State.Idle,"Idle");
            kc.Animations.Add(State.laying,"Prostrate");
            kc.Animations.Add(State.Walk,"Run");
            kc.Animations.Add(State.Turn,"Map Walk");
            kc.Animations.Add(State.Teleport,"Fall");
            kc.Animations.Add(State.Jump,"Airborne");

            if(GlobalSettings.SelectedSkinOption == 2)
            {
                ApplyCustomSkin(knight);
            }

            UnityEngine.Object.DontDestroyOnLoad(knight);

            knight.SetActive(true);
            return knight;
        }

        private void ApplyCustomSkin(GameObject knight)
        {
            try
            {
                if (knight == null)
                {
                    LogError("Knight is null, cannot apply skin");
                    return;
                }

                string modPath = Path.GetDirectoryName(typeof(WiddleKnight).Assembly.Location);
                string skinsPath = Path.Combine(modPath, "Skins");
                
                if (!Directory.Exists(skinsPath))
                {
                    LogError("Skins folder not found");
                    return;
                }

                string[] skinFolders = Directory.GetDirectories(skinsPath);
                
                if (skinFolders.Length == 0 || GlobalSettings.CustomSubOption >= skinFolders.Length)
                {
                    LogError("No valid skin folder selected");
                    return;
                }

                string selectedSkinFolder = skinFolders[GlobalSettings.CustomSubOption];
                string knightPngPath = Path.Combine(selectedSkinFolder, "Knight.png");

                if (!File.Exists(knightPngPath))
                {
                    LogError($"Knight.png not found in {selectedSkinFolder}");
                    return;
                }

                byte[] textureBytes = File.ReadAllBytes(knightPngPath);
                Texture2D customTexture = new Texture2D(2, 2);
                customTexture.LoadImage(textureBytes);

                var spriteAnimator = knight.GetComponent<tk2dSpriteAnimator>();
                if (spriteAnimator == null || spriteAnimator.Library == null)
                {
                    LogError("Sprite animator or library is null");
                    return;
                }

                string cacheKey = $"{selectedSkinFolder}";
                
                if (!cachedLibraries.ContainsKey(cacheKey))
                {
                    Log($"Creating cached library for {cacheKey}");

                    var newLibrary = Object.Instantiate(spriteAnimator.Library);
                    
                    UnityEngine.Object.DontDestroyOnLoad(newLibrary);

                    var materialMap = new Dictionary<Material, Material>();

                    foreach (var clip in newLibrary.clips)
                    {
                        if (clip != null && clip.frames != null)
                        {
                            foreach (var frame in clip.frames)
                            {
                                if (frame != null && frame.spriteCollection != null)
                                {
                                    var newCollection = Object.Instantiate(frame.spriteCollection);
                                    
                                    UnityEngine.Object.DontDestroyOnLoad(newCollection);
                                    
                                    frame.spriteCollection = newCollection;
                                    
                                    var spriteDefinitions = newCollection.spriteDefinitions;
                                    if (spriteDefinitions != null)
                                    {
                                        for (int i = 0; i < spriteDefinitions.Length; i++)
                                        {
                                            if (spriteDefinitions[i] != null && spriteDefinitions[i].material != null)
                                            {
                                                var originalMaterial = spriteDefinitions[i].material;

                                                if (!materialMap.ContainsKey(originalMaterial))
                                                {
                                                    Material newMaterial = new Material(originalMaterial);
                                                    newMaterial.mainTexture = customTexture;
                                                    
                                                    UnityEngine.Object.DontDestroyOnLoad(newMaterial);
                                                    UnityEngine.Object.DontDestroyOnLoad(customTexture);
                                                    
                                                    materialMap[originalMaterial] = newMaterial;
                                                }
                                                
                                                spriteDefinitions[i].material = materialMap[originalMaterial];
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    
                    cachedLibraries[cacheKey] = newLibrary;
                }
                
                spriteAnimator.Library = cachedLibraries[cacheKey];

                Log($"Applied custom skin from {selectedSkinFolder}");
            }
            catch (System.Exception e)
            {
                LogError($"Error applying custom skin: {e.Message}");
            }
        }

        public override void Initialize()
        {
            Instance = this;
            ModHooks.HeroUpdateHook += update;
            if (WiddleKnight.HasPouch())
            {
                PouchIntegration.Initialize();
            }
        }


        public GameObject GetNetworkWiddleKnight(ushort id){
            if(remoteKnights.TryGetValue(id,out var knight)){
                return knight;
            }
            remoteKnights[id] = createKnightcompanion();
            return remoteKnights[id];
        }
        public void update()
        {
            if(GlobalSettings.SelectedSkinOption == 0) {
                return;
            }
            
            knights.RemoveAll(k => k == null);
            
            if(knights.Count < 1) {
                knights.Add(createKnightcompanion());
            }
        }

        public void OnOptionChanged()
        {
            var knightsCopy = new List<GameObject>(knights);
            knights.Clear();

            foreach(var knight in knightsCopy)
            {
                if(knight != null)
                {
                    try
                    {
                        UnityEngine.Object.Destroy(knight);
                    }
                    catch (System.Exception e)
                    {
                        LogError($"Error destroying knight: {e.Message}");
                    }
                }
            }

            var remoteKnightsCopy = new Dictionary<ushort, GameObject>(remoteKnights);
            remoteKnights.Clear();

            foreach(var kvp in remoteKnightsCopy)
            {
                if(kvp.Value != null)
                {
                    try
                    {
                        UnityEngine.Object.Destroy(kvp.Value);
                    }
                    catch (System.Exception e)
                    {
                        LogError($"Error destroying remote knight: {e.Message}");
                    }
                }
            }
            
            cachedLibraries.Clear();
        }

        public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates)
        {
            return WiddleKnightMenu.GetMenu(modListMenu);
        }

        public bool ToggleButtonInsideMenu => false;

    }

}
