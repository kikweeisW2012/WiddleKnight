
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Satchel.EnemyUtils;
using static Satchel.GameObjectUtils;

namespace WiddleKnight
{
    public enum State {
        Idle = 0,
        laying,
        Turn,
        Walk,
        Jump,
        Teleport
    }
    public enum Direction {
        Left = 0,
        Right
    }
    public class WiddleKnightControl : MonoBehaviour
    {

        public bool isNetworkControlled = false;
        
        public tk2dSpriteAnimator animator;
        public Rigidbody2D rb;
        public BoxCollider2D collider;
        public AudioSource audioSource;

        public float scale = 0.6f;
        // needs to be 10.5 or its glitchy
        public float moveSpeed = 10.5f;
        public float IdleShuffleDistance = 0.1f;

        public GameObject followTarget;
        public float followDistance = 2f;
        public float teleportDistance = 10f;

        public AudioClip teleport,walk,yay;
        public Dictionary<State,string> Animations = new Dictionary<State,string>();

        public float followClipChance = 0.03f, teleportClipChance =  0.60f,turnClipChance = 30f, yayClipChance = 0.01f;
        internal State state = State.Idle;
        internal Direction lookDirection = Direction.Left;

        private bool changeDirection = false;
        internal bool moveToNext = true;
        private float idleTimer = 0f;
        private const float LAYING_IDLE_TIME = 10f;
        
        public GameObject getFollowTarget(){
            if(followTarget == null){
                followTarget = HeroController.instance.gameObject;
            }
            return followTarget;
        }

        void Start(){
            collider = gameObject.GetComponent<BoxCollider2D>();
            rb = gameObject.GetAddComponent<Rigidbody2D>();
            animator = gameObject.GetComponent<tk2dSpriteAnimator>();
            audioSource = gameObject.GetAddComponent<AudioSource>();

            rb.bodyType = RigidbodyType2D.Dynamic;
            collider.isTrigger = false;
            gameObject.SetScale(scale,scale);
            StartCoroutine(MainLoop());
        }

        public void playAnim(string clip,bool onlyNew = false) {
            if(animator != null){
                if(!onlyNew || !animator.IsPlaying(clip)){
                    animator.PlayFromFrame(clip, 0);
                }
            }
        }

        public void playAnimForState(bool onlyNew = false){
            if(Animations.TryGetValue(state, out var anim)){
                playAnim(anim,onlyNew);
            } else if(Animations.TryGetValue(State.Idle, out var fallbackAnim)){
                playAnim(fallbackAnim,onlyNew);
            }
        }

        private bool heroFartherThan(float distance){
            var displacement = getFollowTarget().transform.position - transform.position;
            return displacement.magnitude > distance;
        }

        private bool heroFartherThanY(float distance){
            var displacement = getFollowTarget().transform.position - transform.position;
            return displacement.y > distance;
        }

        private bool UpdateLookDirection(){
           Direction newLookDirection;
           changeDirection = false;

           if(getFollowTarget().transform.position.x > transform.position.x){
               newLookDirection = Direction.Right;
           } else {
               newLookDirection = Direction.Left;
           }
           if(newLookDirection != lookDirection){
               changeDirection = true;
               lookDirection = newLookDirection;
           }
           return changeDirection;
        }

        private void fixRotation(){
            gameObject.transform.rotation = Quaternion.identity;
        }

        private IEnumerator TurnToHero(){
           fixRotation();
           playAnimForState();
           if(teleport != null && !audioSource.isPlaying && Random.Range(0.0f, 1.0f) < turnClipChance){
               audioSource.PlayOneShot(teleport);
           }
           yield return new WaitForSeconds(0.5f);
           var ls = gameObject.transform.localScale;
           ls.x = (lookDirection == Direction.Left? 1f : -1f)*Mathf.Abs(ls.x);
           gameObject.transform.localScale = ls;
           state = State.Idle;
           moveToNext = true;
        }

        private void decideNextState(){
            var shouldFollowTarget = true;
            if(getFollowTarget() == HeroController.instance){
                shouldFollowTarget = HeroController.instance.cState.onGround || Random.Range(0.0f, 1.0f) < 0.3f;
            }

            if(UpdateLookDirection()){
                state = State.Turn;
            } else if(heroFartherThan(teleportDistance) && shouldFollowTarget){
                state = State.Teleport;
            } else if(heroFartherThan(followDistance) && shouldFollowTarget){
                if(heroFartherThanY(0.5f) && Random.Range(0.0f, 1.0f) < 0.3f){
                    state = State.Jump;
                } else {
                    state = State.Walk;
                }
            } else {
                state = State.Idle;
            }
        }
        private bool isHeroSittingOnBench(){
            var hero = HeroController.instance;
            if(hero == null) return false;
            return hero.cState.onGround && !hero.cState.dashing && !hero.cState.attacking && Mathf.Abs(hero.GetComponent<Rigidbody2D>().velocity.x) < 0.1f;
        }

        private IEnumerator Idle(){
            fixRotation();
            playAnimForState(true);
            if(yay != null && !audioSource.isPlaying && Random.Range(0.0f, 1.0f) < yayClipChance){
                audioSource.PlayOneShot(yay);
            }
            rb.velocity = new Vector2(0.0f, 0.0f);
            
            var lastState = state;
            decideNextState();
            
            float waitTime = 0.1f;
            
            if(state == State.Idle && lastState == State.laying){
                state = lastState;
                waitTime = 0.1f;
            } else if(lastState == State.Idle){
                idleTimer += waitTime;

                if(idleTimer >= LAYING_IDLE_TIME){
                    state = State.laying;
                    idleTimer = 0f;
                }
                waitTime = 0.1f;
            } else {
                if(!(lastState == state && state == State.Walk)){
                    waitTime = 1f;
                }
                idleTimer = 0f;
            }
            
            yield return new WaitForSeconds(waitTime);
            moveToNext = true;

        }
        private IEnumerator Teleport(){
            fixRotation();
            if(teleport != null && !audioSource.isPlaying && Random.Range(0.0f, 1.0f) < teleportClipChance){
                audioSource.PlayOneShot(teleport);
            }
            playAnimForState();
            var deltaToPlayer = Random.Range(-0.5f, 0.5f);
            if(Random.Range(0.0f, 1.0f) < 0.50f){
                gameObject.transform.position = getFollowTarget().transform.position + new Vector3(0.5f + deltaToPlayer,0f,0f);
            } else {
                gameObject.transform.position = getFollowTarget().transform.position + new Vector3(-0.5f + deltaToPlayer,0f,0f);
            }
            yield return new WaitForSeconds(0.1f);

            state = State.Idle;
            moveToNext = true;
        }

        private IEnumerator Follow(){
            playAnimForState();
            if(walk != null && !audioSource.isPlaying && Random.Range(0.0f, 1.0f) < followClipChance){
                audioSource.PlayOneShot(walk);
            }

            Vector2 displacement;
            displacement = getFollowTarget().transform.position - transform.position;
            displacement += new Vector2(Random.Range(-0.01f, 0.01f),Random.Range(-0.01f, 0.01f));
            if(state == State.Jump){
                displacement += new Vector2(0,3f);
            }
            var followDistanceR = followDistance * (1f+Random.Range(-0.25f, 0.50f));
            var distance = Mathf.Min(teleportDistance,displacement.magnitude-followDistanceR);
            var moveSpeedR = moveSpeed * (1f+Random.Range(-0.25f, 0.50f));
            
            yield return rb.moveTowards(displacement, distance, distance/moveSpeedR);

            state = State.Idle;
            moveToNext = true;
        }
        public Vector2 networkMovementTarget = new Vector2(0,0);
        
        public Coroutine networkCoro;
        private IEnumerator ApplyNetworkState(){
            yield return null;
            playAnimForState();
            var ls = gameObject.transform.localScale;
            ls.x = (lookDirection == Direction.Left? 1f : -1f)*Mathf.Abs(ls.x);
            gameObject.transform.localScale = ls;
            var value = 0.1f;
            if(state == State.Teleport){
                transform.position = networkMovementTarget;
            } else {
                while((Vector2)transform.position != networkMovementTarget){
                    transform.position = Vector2.Lerp(transform.position, networkMovementTarget, value);
                    value += 0.1f;
                    yield return new WaitForSeconds(0.02f);
                }
            }
        }
        internal void UpdateNetworkCoro(){
            if(networkCoro != null){
                StopCoroutine(networkCoro);
            }
            networkCoro = StartCoroutine(ApplyNetworkState());
        }

        private IEnumerator MainLoop(){
            while(true){
                yield return new WaitWhile(()=>!moveToNext);
                moveToNext = false;
                if (isNetworkControlled)
                {
                    continue;
                }
                if (WiddleKnight.HasPouch()) { 
                    PouchIntegration.SendUpdate(this);
                }

                if (state == State.Idle || state == State.laying){
                    StartCoroutine(Idle());
                } else if(state == State.Walk){
                    StartCoroutine(Follow());
                } else if(state == State.Jump){
                    StartCoroutine(Follow());
                } else if(state == State.Teleport){
                    StartCoroutine(Teleport());
                } else if(state == State.Turn){
                    StartCoroutine(TurnToHero());
                }
            }
        }
    }
}