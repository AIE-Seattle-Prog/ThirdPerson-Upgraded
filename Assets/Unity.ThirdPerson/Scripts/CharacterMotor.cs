using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

/* Note: animations are called via the motor */

namespace Unity.StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    [SelectionBase]
    public class CharacterMotor : MonoBehaviour
    {
        [Header("Player")]
        [Tooltip("Move speed of the character in m/s")]
        public float MoveSpeed = 2.0f;

        [Tooltip("Sprint speed of the character in m/s")]
        public float SprintSpeed = 5.335f;

        [Tooltip("How fast the character turns to face movement direction")]
        [Range(0.0f, 0.3f)]
        public float RotationSmoothTime = 0.12f;

        [Tooltip("Acceleration and deceleration")]
        public float SpeedChangeRate = 10.0f;

        public AudioClip LandingAudioClip;
        public AudioClip[] FootstepAudioClips;
        [Range(0, 1)] public float FootstepAudioVolume = 0.5f;

        [Space(10)]
        [Tooltip("The height the player can jump")]
        public float JumpHeight = 1.2f;

        [Tooltip("The character uses its own gravity value. The engine default is -9.81f")]
        public float Gravity = -15.0f;

        [Space(10)]
        [Tooltip("Time required to pass before being able to jump again. Set to 0f to instantly jump again")]
        public float JumpTimeout = 0.50f;

        [Tooltip("Time required to pass before entering the fall state. Useful for walking down stairs")]
        public float FallTimeout = 0.15f;

        [Header("Player Grounded")]

        [Tooltip("Useful for rough ground")]
        public float GroundedOffset = -0.14f;

        [Tooltip("The radius of the grounded check. Should match the radius of the CharacterController")]
        public float GroundedRadius = 0.28f;

        public bool Grounded => CurrentMoveState == MoveState.Grounded;

        [Tooltip("What layers the character uses as ground")]
        public LayerMask GroundLayers;

        public enum MoveState
        {
            Grounded,
            Falling,
            Floating,
            Hanging
        }
        public MoveState CurrentMoveState { get; private set; } = MoveState.Falling;

        [Header("Player Ledge")]
        public float LedgeOffset = 0.14f;
        
        public float LedgeHangOffset = 0.5f;

        public float LedgeRadius = 0.28f;

        private Collider LedgeVolume;

        public LayerMask LedgeLayers;

        private Vector3 LedgeHangPosition;


        [Header("Sub-Components")]
        [SerializeField]
        private Animator _animator;
        [SerializeField]
        private CharacterController _controller;

        // player
        private float _speed;
        private float _animationBlend;
        private float _targetRotation = 0.0f;
        private float _rotationVelocity;
        private float _verticalVelocity;
        private float _terminalVelocity = 53.0f;

        // timeout deltatime
        private float _jumpTimeoutDelta;
        private float _fallTimeoutDelta;

        // animation IDs
        private int _animIDSpeed;
        private int _animIDGrounded;
        private int _animIDJump;
        private int _animIDFreeFall;
        private int _animIDMotionSpeed;
        private int _animIDHanging;

        private Transform _transform;

        private bool _hasAnimator;

        // inputs
        [NonSerialized] public bool JumpWish;
        [NonSerialized] public bool CrouchWish;
        [NonSerialized] public bool SprintWish;
        [NonSerialized] public Vector3 RawMoveWish;
        [NonSerialized] public Vector3 MoveWish;

        private void Awake()
        {
            _transform = transform;
            if (!_animator)
            {
                TryGetComponent(out _animator);
            }
            _hasAnimator = _animator != null;

            if (!_controller)
            {
                _controller = GetComponent<CharacterController>();
            }

            AssignAnimationIDs();
        }

        private void Start()
        {
            // reset our timeouts on start
            _jumpTimeoutDelta = JumpTimeout;
            _fallTimeoutDelta = FallTimeout;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F))
            {
                CurrentMoveState = CurrentMoveState == MoveState.Floating ? MoveState.Falling : MoveState.Floating;
            }

            switch (CurrentMoveState)
            {
                case MoveState.Grounded:
                    JumpAndGravity();
                    GroundedCheck();
                    GroundMove();
                    break;
                case MoveState.Falling:
                    JumpAndGravity();
                    LedgeCheck();
                    if (CurrentMoveState != MoveState.Falling) { break; }
                    GroundedCheck();
                    GroundMove();
                    break;
                case MoveState.Hanging:
                    Hang();
                    HangMove();
                    break;
                case MoveState.Floating:
                    Fly();
                    GroundMove();
                    break;
            }
        }

        private void LedgeCheck()
        {
            if (_verticalVelocity < 0.0f) { return; }

            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y + LedgeOffset, transform.position.z);
            Collider[] overlaps = Physics.OverlapSphere(spherePosition, LedgeRadius, LedgeLayers, QueryTriggerInteraction.Collide);

            if (overlaps.Length > 0)
            {
                CurrentMoveState = MoveState.Hanging;

                LedgeVolume = overlaps[0];
                LedgeHangPosition = LedgeVolume.ClosestPoint(transform.position + Vector3.up * 10.0f);

                _animator.SetBool(_animIDHanging, true);
            }
        }

        private void Hang()
        {
            if (JumpWish)
            {
                CurrentMoveState = MoveState.Falling;

                _animator.SetBool(_animIDHanging, false);
                return;
            }

            float targetSpeed = SprintWish ? SprintSpeed : MoveSpeed;

            Vector3 targetDirection = Vector3.Cross(Vector3.up, -LedgeVolume.transform.forward) * (RawMoveWish.x * targetSpeed * Time.deltaTime);
            Vector3 wishPosition = LedgeHangPosition + targetDirection;

            LedgeHangPosition = LedgeVolume.ClosestPoint(wishPosition + Vector3.up * 10.0f);

            Vector3 ledgePose = LedgeHangPosition - (Vector3.up * _controller.height) - (Vector3.up * LedgeHangOffset);
            Vector3 offsetToPose = ledgePose - transform.position;

            float snapSpeed = 10.0f;

            Vector3 displacement = offsetToPose.normalized * snapSpeed;

            if (offsetToPose.magnitude > 0.1f)
            {
                _verticalVelocity = displacement.y;
            }
            else
            {
                _verticalVelocity = 0.0f;
            }
        }

        private void AssignAnimationIDs()
        {
            _animIDSpeed = Animator.StringToHash("Speed");
            _animIDGrounded = Animator.StringToHash("Grounded");
            _animIDJump = Animator.StringToHash("Jump");
            _animIDFreeFall = Animator.StringToHash("FreeFall");
            _animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
            _animIDHanging = Animator.StringToHash("Hanging");
        }

        private void GroundedCheck()
        {
            // set sphere position, with offset
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - GroundedOffset,
                transform.position.z);
            bool isGrounded = Physics.CheckSphere(spherePosition, GroundedRadius, GroundLayers,
                QueryTriggerInteraction.Ignore);

            CurrentMoveState = isGrounded ? MoveState.Grounded : MoveState.Falling;

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetBool(_animIDGrounded, Grounded);
            }
        }

        private void GroundMove()
        {
            Vector2 moveWishXZ = new Vector2(MoveWish.x, MoveWish.z);
            // set target speed based on move speed, sprint speed and if sprint is pressed
            float targetSpeed = SprintWish ? SprintSpeed : MoveSpeed;

            // a simplistic acceleration and deceleration designed to be easy to remove, replace, or iterate upon

            // note: Vector2's == operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is no input, set the target speed to 0
            if (moveWishXZ == Vector2.zero) targetSpeed = 0.0f;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;
            float inputMagnitude = Vector2.ClampMagnitude(moveWishXZ, 1).magnitude;

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // normalise input direction
            Vector3 inputDirection = new Vector3(moveWishXZ.x, 0.0f, moveWishXZ.y).normalized;

            // note: Vector2's != operator uses approximation so is not floating point error prone, and is cheaper than magnitude
            // if there is a move input rotate player when the player is moving
            if (moveWishXZ != Vector2.zero)
            {
                _targetRotation = Mathf.Atan2(inputDirection.x, inputDirection.z) * Mathf.Rad2Deg;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                    RotationSmoothTime);

                // rotate to face input direction
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }


            Vector3 targetDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void HangMove()
        {
            float targetSpeed = SprintWish ? SprintSpeed : MoveSpeed;

            Vector3 ledgePose = LedgeHangPosition - (Vector3.up * _controller.height);
            Vector3 offsetToPose = ledgePose - transform.position;

            // a reference to the players current horizontal velocity
            float currentHorizontalSpeed = new Vector3(_controller.velocity.x, 0.0f, _controller.velocity.z).magnitude;

            float speedOffset = 0.1f;

            float inputMagnitude = Mathf.Max(Mathf.Abs(RawMoveWish.x), 0);

            // accelerate or decelerate to target speed
            if (currentHorizontalSpeed < targetSpeed - speedOffset ||
                currentHorizontalSpeed > targetSpeed + speedOffset)
            {
                // creates curved result rather than a linear one giving a more organic speed change
                // note T in Lerp is clamped, so we don't need to clamp our speed
                _speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed,
                    Time.deltaTime * SpeedChangeRate);

                // round speed to 3 decimal places
                _speed = Mathf.Round(_speed * 1000f) / 1000f;
            }
            else
            {
                _speed = targetSpeed;
            }

            Vector3 targetDirection = offsetToPose.normalized;
            targetDirection.y = 0.0f;

            float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity,
                RotationSmoothTime);

            // move the player
            _controller.Move(targetDirection.normalized * (_speed * Time.deltaTime) +
                             new Vector3(0.0f, _verticalVelocity, 0.0f) * Time.deltaTime);

            // rotate to face ledge direction
            transform.rotation = Quaternion.LookRotation(-LedgeVolume.transform.forward);

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, Time.deltaTime * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            // update animator if using character
            if (_hasAnimator)
            {
                _animator.SetFloat(_animIDSpeed, _animationBlend);
                _animator.SetFloat(_animIDMotionSpeed, inputMagnitude);
            }
        }

        private void JumpAndGravity()
        {
            if (Grounded)
            {
                // reset the fall timeout timer
                _fallTimeoutDelta = FallTimeout;

                // update animator if using character
                if (_hasAnimator)
                {
                    _animator.SetBool(_animIDJump, false);
                    _animator.SetBool(_animIDFreeFall, false);
                }

                // stop our velocity dropping infinitely when grounded
                if (_verticalVelocity < 0.0f)
                {
                    _verticalVelocity = -2f;
                }

                // Jump
                if (JumpWish && _jumpTimeoutDelta <= 0.0f)
                {
                    // the square root of H * -2 * G = how much velocity needed to reach desired height
                    _verticalVelocity = Mathf.Sqrt(JumpHeight * -2f * Gravity);

                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDJump, true);
                    }
                }

                // jump timeout
                if (_jumpTimeoutDelta >= 0.0f)
                {
                    _jumpTimeoutDelta -= Time.deltaTime;
                }
            }
            else
            {
                // reset the jump timeout timer
                _jumpTimeoutDelta = JumpTimeout;

                // fall timeout
                if (_fallTimeoutDelta >= 0.0f)
                {
                    _fallTimeoutDelta -= Time.deltaTime;
                }
                else
                {
                    // update animator if using character
                    if (_hasAnimator)
                    {
                        _animator.SetBool(_animIDFreeFall, true);
                    }
                }
            }

            // clear jump flag
            JumpWish = false;

            // apply gravity over time if under terminal (multiply by delta time twice to linearly speed up over time)
            if (_verticalVelocity < _terminalVelocity)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
            }
        }

        private void Fly()
        {
            float flyAxis = 0.0f;
            if (JumpWish) { flyAxis += 1.0f; }
            if (CrouchWish) { flyAxis -= 1.0f; }

            _verticalVelocity = flyAxis * MoveSpeed;
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (Grounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z),
                GroundedRadius);

            if (CurrentMoveState == MoveState.Falling) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            // when selected, draw a gizmo in the position of, and matching radius of, the grounded collider
            Gizmos.DrawSphere(
                new Vector3(transform.position.x, transform.position.y + LedgeOffset, transform.position.z),
                LedgeRadius);


            if (CurrentMoveState == MoveState.Hanging)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawSphere(LedgeHangPosition, 0.1f);

                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(LedgeHangPosition - (Vector3.up * _controller.height), 0.1f);
            }
        }

        private void OnFootstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                if (FootstepAudioClips.Length > 0)
                {
                    var index = Random.Range(0, FootstepAudioClips.Length);
                    AudioSource.PlayClipAtPoint(FootstepAudioClips[index], transform.TransformPoint(_controller.center), FootstepAudioVolume);
                }
            }
        }

        private void OnLand(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > 0.5f)
            {
                AudioSource.PlayClipAtPoint(LandingAudioClip, transform.TransformPoint(_controller.center), FootstepAudioVolume);
            }
        }
    }
}