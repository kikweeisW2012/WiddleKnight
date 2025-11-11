using Modding;
using System.Collections.Generic;
using UnityEngine;
using static Satchel.EnemyUtils;
using static Satchel.GameObjectUtils;

namespace Konpanion
{
    public class Konpanion : Mod
    {

        internal static Konpanion Instance;

        internal static List<GameObject> knights = new List<GameObject>();
        internal static Dictionary<ushort,GameObject> remoteKnights = new Dictionary<ushort,GameObject>();

        public static bool HasPouch()
        {
            var hasPouch = ModHooks.GetMod("HkmpPouch") is Mod;
            return hasPouch;
        }
        public override string GetVersion()
        {
            return "0.2.1.0";
        }

        public GameObject createKnightCompanion(GameObject ft = null){
            HeroController.instance.gameObject.SetActive(false);
            var knight = HeroController.instance.gameObject.createCompanionFromPrefab();
            HeroController.instance.gameObject.SetActive(true);
            knight.RemoveComponent<HeroController>();
            knight.RemoveComponent<HeroAnimationController>();
            knight.RemoveComponent<HeroAudioController>();
            knight.RemoveComponent<ConveyorMovementHero>();

            knight.name = "kompanion";
            knight.GetAddComponent<MeshRenderer>().enabled = true;
            knight.GetAddComponent<Rigidbody2D>().gravityScale = 1f;

            //add control and adjust parameters
            var kc = knight.GetAddComponent<CompanionControl>();
            kc.moveSpeed = 10f;
            kc.followDistance = 2f;
            kc.IdleShuffleDistance = 0.01f;

            if(ft != null){
                kc.followTarget = ft;
            }

            //fix up collider size
            var collider = knight.GetAddComponent<BoxCollider2D>();
            collider.size = new Vector2(1f,2.0f);
            collider.offset = new Vector2(0f,-0.4f);
            collider.enabled = true;

            // add animations
            kc.Animations.Add(State.Idle,"Idle");
            kc.Animations.Add(State.IdleFidget1,"Acid Death");
            kc.Animations.Add(State.IdleFidget2,"Prostrate");
            kc.Animations.Add(State.Walk,"Run");
            kc.Animations.Add(State.Turn,"Map Walk");
            kc.Animations.Add(State.Teleport,"Fall");
            kc.Animations.Add(State.Jump,"Airborne");

            knight.SetActive(true);
            return knight;
        }

        public override void Initialize()
        {
            Instance = this;
            ModHooks.HeroUpdateHook += update;
            if (Konpanion.HasPouch())
            {
                PouchIntegration.Initialize();
            }
        }


        public GameObject GetNetworkKonpanion(ushort id){
            if(remoteKnights.TryGetValue(id,out var knight)){
                return knight;
            }
            remoteKnights[id] = createKnightCompanion();
            return remoteKnights[id];
        }
        public void update()
        {
            if(knights.Count < 1) {
                knights.Add(createKnightCompanion());
            }
        }

    }

}
