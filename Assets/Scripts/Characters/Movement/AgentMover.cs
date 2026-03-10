// Файл: Assets/Scripts/Characters/Movement/AgentMover.cs
using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Required for List<>
using System.Linq; // May be needed if more complex logic is added
using Characters;

[RequireComponent(typeof(Rigidbody2D))] // Ensure Rigidbody2D is present
public class AgentMover : MonoBehaviour
{
    [Header("Параметры Движения")]
    public float moveSpeed = 2f; // Base movement speed
    public float stoppingDistance = 0.2f; // How close to a waypoint to consider it reached

    [Header("Простая анимация ходьбы")]
    public SpriteRenderer characterSpriteRenderer; // Renderer for the main body sprite
    public Sprite idleSprite; // Sprite when standing still
    public Sprite walkSprite1; // First walking frame
    public Sprite walkSprite2; // Second walking frame
    public float animationSpeed = 0.3f; // Time (in seconds) between walk frame changes
	[Tooltip("Угол покачивания при ходьбе (в градусах). Например, 5.")]
    public float walkWaddleAngle = 3f;
    private float animationTimer = 0f;
    private bool isFirstWalkSprite = true;

    [Header("Динамический свет")]
    [Tooltip("Перетащите сюда Transform объекта Spotlight")]
    [SerializeField] private Transform dynamicLight; // Optional light attached to the character
    [Tooltip("На какое расстояние свет будет смещаться вперед при движении")]
    [SerializeField] public float lightForwardOffset = 0.5f; // How far the light moves ahead
    [Tooltip("Насколько плавно свет возвращается на место и следует за движением")]
    [SerializeField] private float lightSmoothing = 5f; // Smoothness of light movement

    // --- Возвращено: Недостающие поля из вашей версии ---
    private float baseMoveSpeed; // Stores the initial moveSpeed for multipliers
    // public UnityEngine.AI.NavMeshAgent agent; // If using NavMeshAgent (currently seems Rigidbody2D based)
    [Header("Система 'Резиночки' (Path Following)")]
    [Tooltip("Насколько сильно персонаж стремится вернуться на свой путь.")]
    public float rubberBandStrength = 5f; // How strongly the agent pulls towards the path anchor
    [Tooltip("Приоритет персонажа. Решает, кто кого продавливает при столкновении.")]
    public int priority = 1; // Higher priority agents push lower priority ones

    // --- Компоненты и Внутренние переменные ---
    private Rigidbody2D rb; // Reference to the Rigidbody2D component
    private Queue<Waypoint> path; // Current path the agent is following
    private Vector2 pathAnchor; // A point that moves along the path segments, agent follows this
    private bool isYielding = false; // Flag if agent is currently yielding to another agent
    private Coroutine yieldingCoroutine; // Coroutine handle for yielding
    private float dirtTimer = 0f; // Timer for dirt generation
    private float dirtInterval = 0.25f; // How often to generate dirt while moving

    // --- Прямое преследование ---
    private bool isDirectChasing = false; // Flag if agent is chasing a specific point directly
    private Vector2 directChaseTarget; // The target position for direct chase
    [Header("Настройки преследования")]
    [Tooltip("С какого расстояния персонаж начнет замедляться при прямой погоне.")]
    public float slowingDistance = 2.0f; // Distance to start slowing down during direct chase
    [Tooltip("Насколько плавно персонаж меняет скорость.")]
    public float movementSmoothing = 5f; // Smoothing factor for velocity changes

    // --- Звуки шагов ---
    [Header("Звуки шагов")]
    [Tooltip("Список из 6 (или больше) звуков шагов для случайного выбора")]
    public List<AudioClip> footstepSounds;
    [Tooltip("Компонент AudioSource для проигрывания шагов. Если не указан, будет найден или добавлен автоматически.")]
    public AudioSource footstepAudioSource;
    [Tooltip("Минимальная скорость Rigidbody, при которой воспроизводятся шаги")]
    public float minSpeedForFootsteps = 0.5f;
    [Tooltip("Примерное расстояние между шагами при базовой скорости (moveSpeed). Чем меньше, тем чаще шаги.")]
    public float distanceBetweenSteps = 0.8f; // Base distance in units between steps
    // Внутренние переменные для отслеживания шагов
    private float distanceCoveredSinceLastStep = 0f;
    private Vector3 lastPositionForSteps;

    // --- Настройки Падения ---
    [Header("Настройки Падения")]
    [Tooltip("Точка опоры при падении (должна быть между ног на уровне пола)")]
    public Transform fallPivotPoint;
    [Tooltip("Звук, проигрываемый при падении")]
    public AudioClip fallSound;
    private bool isSlipping = false; // Flag indicating the character is currently falling/recovering
    public bool IsSlipping => isSlipping; // Public getter for the slipping state

    // --- Система "Втягивания живота" при столкновении ---
    private Collider2D mainCollider;
    private float originalColliderSize;
    private float squeezeTimer = 0f;
    private float squeezeRecoveryTimer = 0f;
    // ----------------------------------------------------

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) {
            Debug.LogError($"Rigidbody2D не найден на {gameObject.name}!", gameObject);
            enabled = false; // Disable script if Rigidbody is missing
            return;
        }
        // Ensure Rigidbody settings are appropriate (e.g., Gravity Scale if needed, Freeze Rotation Z)
        rb.freezeRotation = true; // Usually characters shouldn't rotate via physics

        pathAnchor = transform.position; // Initialize path anchor to current position
        baseMoveSpeed = moveSpeed; // Store the initial speed
        lastPositionForSteps = transform.position; // Initialize position for footstep tracking

        // --- Получаем или добавляем AudioSource для шагов ---
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                // Add AudioSource if not found
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
                footstepAudioSource.playOnAwake = false;
                footstepAudioSource.spatialBlend = 1.0f; // Make sound 3D
                footstepAudioSource.volume = 0.4f; // Default volume, adjust as needed
                // Configure other AudioSource settings (rolloff, doppler) if desired
                Debug.LogWarning($"AudioSource для шагов добавлен автоматически к {gameObject.name}. Настройте его параметры (громкость, 3D Sound Settings) при необходимости.");
            }
        }

        // --- Инициализация коллайдера для системы "Втягивания живота" ---
        mainCollider = GetComponent<Collider2D>();
        if (mainCollider != null)
        {
            if (mainCollider is CircleCollider2D circle)
            {
                originalColliderSize = circle.radius;
            }
            else if (mainCollider is CapsuleCollider2D capsule)
            {
                originalColliderSize = capsule.size.x;
            }
        }
        // ----------------------------------------------------------------
    }


    // Update is called once per frame, good for non-physics related checks like footstep distance
    void Update()
    {
        // Handle footstep sounds based on distance moved
        HandleFootsteps();

        // Handle visual updates like animation and dynamic light
        HandleWalkAnimation();
        UpdateDynamicLightPosition();
    }

    // FixedUpdate is called at a fixed interval, best for physics calculations
    void FixedUpdate()
    {
        // If the character is currently slipping/recovering, skip movement physics
        if (isSlipping)
        {
             // Gradually reduce velocity while slipping/lying down
             rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, Vector2.zero, Time.fixedDeltaTime * movementSmoothing * 2f); // Increased damping
             return; // Skip the rest of FixedUpdate
        }

        Vector2 desiredVelocity; // The velocity the agent wants to achieve this frame

        // --- Calculate Desired Velocity based on mode ---
        if (isDirectChasing)
        {
            // Direct Chase Logic: Move towards directChaseTarget, slowing down near the end
            Vector2 vectorToTarget = directChaseTarget - (Vector2)transform.position;
            float distanceToTarget = vectorToTarget.magnitude;

            // Calculate target speed based on distance (slow down when close)
            float targetSpeed = moveSpeed;
            if (distanceToTarget < slowingDistance && slowingDistance > 0) // Avoid division by zero
            {
                targetSpeed = moveSpeed * (distanceToTarget / slowingDistance);
            }
            // Calculate velocity vector (normalized direction * target speed)
            desiredVelocity = (distanceToTarget > 0.01f) ? vectorToTarget.normalized * targetSpeed : Vector2.zero;
        }
        else if (path == null || path.Count == 0)
        {
            // No Path Logic: Stand still
            desiredVelocity = Vector2.zero;
        }
        else // Path Following Logic
        {
            // Get the next waypoint in the path
            Waypoint targetWaypoint = path.Peek();

            // Move the pathAnchor along the path towards the target waypoint
            // Ensure targetWaypoint and its transform are not null
            if (targetWaypoint != null && targetWaypoint.transform != null) {
                pathAnchor = Vector2.MoveTowards(pathAnchor, targetWaypoint.transform.position, moveSpeed * Time.fixedDeltaTime);
            } else {
                 Debug.LogError($"Целевая точка пути null или ее transform null для {gameObject.name}. Остановка.");
                 Stop(); // Stop if the path is corrupted
                 desiredVelocity = Vector2.zero; // Set velocity to zero for this frame
                 rb.linearVelocity = desiredVelocity;
                 UpdateSpriteDirection(rb.linearVelocity); // Update visuals based on stop
                 HandleDirtLogic();
                 return; // Exit FixedUpdate early
            }


            // Calculate the "rubber band" force pulling the agent towards the pathAnchor
            // Reduce strength if currently yielding
            float currentStrength = isYielding ? rubberBandStrength / 4f : rubberBandStrength;
            desiredVelocity = (pathAnchor - (Vector2)transform.position) * currentStrength;
            // Clamp the magnitude of the velocity to avoid excessive speed spikes
            desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, moveSpeed * 1.5f); // Allow slightly exceeding base speed

            // Check if the agent has reached the current target waypoint
             // Check distance between agent's actual position and waypoint
            if (Vector2.Distance(transform.position, targetWaypoint.transform.position) < stoppingDistance)
            {
                // Snap anchor to the reached waypoint position
                pathAnchor = targetWaypoint.transform.position;
                // Remove the reached waypoint from the queue
                path.Dequeue();
                 // If that was the last waypoint, stop completely in the next frame
                 if (path.Count == 0) {
                     desiredVelocity = Vector2.zero; // Aim to stop
                     // Debug.Log($"Достигнута последняя точка пути для {gameObject.name}.");
                 }
            }
        }
        // --- End Calculate Desired Velocity ---

        // Apply the desired velocity smoothly to the Rigidbody
        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * movementSmoothing);

        // Update sprite direction based on current velocity
        UpdateSpriteDirection(rb.linearVelocity);

        // Handle dirt generation based on movement
        HandleDirtLogic();

        // --- Логика сжатия ("Втягивание живота") в конце FixedUpdate ---
        if (mainCollider != null && originalColliderSize > 0)
        {
            // Задержка сжатия зависит от наглости (priority)
            float squeezeDelay = priority * 0.4f; // Директор/Охранник (priority=5) начнут сжиматься через 2 сек, клиенты/стажеры (priority=1) через 0.4 сек
            
            // Целевой размер
            float targetSize = originalColliderSize;
            
            // Если долго в коллизии - сжимаемся до 30%
            if (squeezeTimer > squeezeDelay)
            {
                targetSize = originalColliderSize * 0.3f;
            }
            
            // Плавно меняем размер коллайдера
            float currentSize = 0f;
            if (mainCollider is CircleCollider2D circle)
            {
                currentSize = circle.radius;
                circle.radius = Mathf.Lerp(currentSize, targetSize, Time.fixedDeltaTime * 5f);
            }
            else if (mainCollider is CapsuleCollider2D capsule)
            {
                currentSize = capsule.size.x;
                capsule.size = new Vector2(Mathf.Lerp(currentSize, targetSize, Time.fixedDeltaTime * 5f), capsule.size.y);
            }
            
            // Уменьшаем таймеры
            if (squeezeRecoveryTimer > 0)
            {
                squeezeRecoveryTimer -= Time.fixedDeltaTime;
            }
            else
            {
                // Плавно уменьшаем squeezeTimer когда не в коллизии
                squeezeTimer = Mathf.Max(0, squeezeTimer - Time.fixedDeltaTime * 0.5f);
            }
        }
        // -----------------------------------------------------------------
    }

    /// <summary>
    /// Calculates distance moved and plays footstep sounds accordingly.
    /// Called from Update.
    /// </summary>
    private void HandleFootsteps()
    {
        // Calculate distance moved since the last Update frame
        float distanceMoved = Vector3.Distance(transform.position, lastPositionForSteps);
        distanceCoveredSinceLastStep += distanceMoved;
        lastPositionForSteps = transform.position; // Update last known position

        // Get current speed from Rigidbody (magnitude of velocity vector)
        float currentSpeed = rb.linearVelocity.magnitude;

        // Check conditions for playing footsteps
        if (!isSlipping && // Don't play steps while falling/lying down
            currentSpeed > minSpeedForFootsteps &&
            footstepSounds != null && footstepSounds.Count > 0 &&
            footstepAudioSource != null)
        {
            // Calculate the required distance for the next step based on current speed
            // Faster speed = smaller distance between steps = more frequent steps
            float speedRatio = Mathf.Max(0.1f, currentSpeed / baseMoveSpeed); // Avoid division by zero, ensure minimum ratio
            float requiredDistance = distanceBetweenSteps / speedRatio;

            // If enough distance has been covered since the last step
            if (distanceCoveredSinceLastStep >= requiredDistance)
            {
                PlayFootstepSound(); // Play a random footstep sound
                // Subtract the distance used by this step (allows carrying over excess distance)
                distanceCoveredSinceLastStep -= requiredDistance;
                 // Ensure it doesn't become significantly negative due to large steps/low framerate
                 distanceCoveredSinceLastStep = Mathf.Max(0f, distanceCoveredSinceLastStep);
            }
        }
        else
        {
            // If standing still or moving very slowly, reset the distance counter
             if (currentSpeed < 0.1f) // Use a small threshold to prevent resetting from tiny movements
                 distanceCoveredSinceLastStep = 0f;
        }
    }


    /// <summary>
    /// Plays a random footstep sound from the list using the assigned AudioSource.
    /// </summary>
    private void PlayFootstepSound()
    {
        // Safety checks
        if (footstepSounds == null || footstepSounds.Count == 0 || footstepAudioSource == null) return;

        // Select a random audio clip from the list
        int randomIndex = Random.Range(0, footstepSounds.Count);
        AudioClip clipToPlay = footstepSounds[randomIndex];

        // Play the selected clip if it's valid
        if (clipToPlay != null)
        {
            // PlayOneShot is suitable for short, potentially overlapping sounds like footsteps
            footstepAudioSource.PlayOneShot(clipToPlay);
        } else {
             Debug.LogWarning($"Выбранный звук шага (индекс {randomIndex}) равен null для {gameObject.name}.");
        }
    }

    /// <summary>
    /// Applies a temporary speed multiplier to the base speed.
    /// </summary>
    /// <param name="multiplier">Speed multiplier (1.0 = normal speed).</param>
    public void ApplySpeedMultiplier(float multiplier)
    {
        moveSpeed = baseMoveSpeed * multiplier;
    }

    /// <summary>
    /// Updates the position of the dynamic light based on movement direction.
    /// Called from Update.
    /// </summary>
    private void UpdateDynamicLightPosition()
    {
        if (dynamicLight == null) return; // Skip if no light assigned

        Vector2 targetPosition;
        // Move light forward if moving and not slipping
        if (rb.linearVelocity.magnitude > 0.1f && !isSlipping)
        {
            // Target position is offset in the direction of velocity
            targetPosition = rb.linearVelocity.normalized * lightForwardOffset;
        }
        else // Otherwise, target position is centered (local zero)
        {
            targetPosition = Vector2.zero;
        }
        // Smoothly move the light towards the target local position
        dynamicLight.localPosition = Vector2.Lerp(dynamicLight.localPosition, targetPosition, Time.deltaTime * lightSmoothing);
    }

    /// <summary>
    /// Handles the simple two-frame walk animation based on movement speed.
    /// Called from Update.
    /// </summary>
    private void HandleWalkAnimation()
    {
        // Проверяем наличие ссылок
        if (characterSpriteRenderer == null || idleSprite == null || walkSprite1 == null || walkSprite2 == null) return;

        // Анимируем, только если движемся и не падаем/лежим
        if (rb.linearVelocity.magnitude > 0.1f && !isSlipping)
        {
            animationTimer += Time.deltaTime;
            
            if (animationTimer >= animationSpeed)
            {
                animationTimer = 0f;
                isFirstWalkSprite = !isFirstWalkSprite;
                characterSpriteRenderer.sprite = isFirstWalkSprite ? walkSprite1 : walkSprite2;

                // --- НОВАЯ ЛОГИКА ПОВОРОТА ---
                // Если первый кадр - поворачиваем в одну сторону, если второй - в другую
                float targetAngle = isFirstWalkSprite ? walkWaddleAngle : -walkWaddleAngle;
                characterSpriteRenderer.transform.localRotation = Quaternion.Euler(0, 0, targetAngle);
                // -----------------------------
            }
        }
        else // Если стоим или упали
        {
            // Сбрасываем спрайт и поворот, ТОЛЬКО если не упали (при падении там своя логика поворота)
            if (!isSlipping)
            {
                 if (rb.linearVelocity.magnitude < 0.05f) 
                 {
                     characterSpriteRenderer.sprite = idleSprite;
                     
                     // --- СБРОС ПОВОРОТА ---
                     characterSpriteRenderer.transform.localRotation = Quaternion.identity;
                     // ----------------------
                 }
            }
            animationTimer = 0f; 
        }
    }


    /// <summary>
    /// Allows external scripts (like CharacterVisuals) to set the sprites used for animation.
    /// </summary>
    public void SetAnimationSprites(Sprite idle, Sprite walk1, Sprite walk2)
{
    Debug.Log($"[AgentMover] {gameObject.name} получил SetAnimationSprites. Idle: {idle?.name}, Walk1: {walk1?.name}, Walk2: {walk2?.name}"); // <<<< ДОБАВЬ ЭТОТ ЛОГ
    this.idleSprite = idle;
    this.walkSprite1 = walk1;
    this.walkSprite2 = walk2;
    if (characterSpriteRenderer != null && rb.linearVelocity.magnitude < 0.1f) {
        characterSpriteRenderer.sprite = this.idleSprite;
    }
}


    // --- Direct Chase Methods ---
    /// <summary>
    /// Starts direct chasing towards a specific world position. Clears any existing path.
    /// </summary>
    public void StartDirectChase(Vector2 targetPosition)
    {
        if (isSlipping) return; // Cannot start chase while slipping
        isDirectChasing = true;
        directChaseTarget = targetPosition;
        path?.Clear(); // Clear existing path
        path = null; // Set path to null
        // Debug.Log($"[AgentMover] {gameObject.name} начал прямое преследование к {targetPosition}.");
    }

    /// <summary>
    /// Updates the target position for direct chasing if currently active.
    /// </summary>
    public void UpdateDirectChase(Vector2 targetPosition)
    {
        if (isDirectChasing && !isSlipping) // Only update if chasing and not slipping
        {
            directChaseTarget = targetPosition;
        }
    }

    /// <summary>
    /// Stops direct chasing mode. Does not stop movement immediately.
    /// </summary>
    public void StopDirectChase()
    {
        isDirectChasing = false;
        // Debug.Log($"[AgentMover] {gameObject.name} прекратил прямое преследование.");
    }
    // --- End Direct Chase ---

    /// <summary>
    /// Adds traffic to the DirtGridManager based on movement.
    /// Called from FixedUpdate.
    /// </summary>
    private void HandleDirtLogic()
    {
        // Generate dirt only if moving significantly and not slipping
        if (rb.linearVelocity.magnitude > 0.1f && !isSlipping)
        {
            dirtTimer += Time.fixedDeltaTime; // Use fixedDeltaTime here
            if (dirtTimer >= dirtInterval)
            {
                dirtTimer = 0f;
                // Safely call DirtGridManager instance
                DirtGridManager.Instance?.AddTraffic(transform.position);
            }
        } else {
             dirtTimer = 0f; // Reset timer if stopped or slipping
        }
    }


    /// <summary>
    /// Sets a new path for the agent to follow. Stops direct chasing.
    /// </summary>
    /// <param name="newPath">The queue of waypoints representing the path.</param>
    public void SetPath(Queue<Waypoint> newPath)
    {
        // Do not accept new path while slipping
        if (isSlipping) {
             Debug.LogWarning($"[AgentMover] {gameObject.name} проигнорировал новый путь из-за падения.");
             return;
        }


        StopDirectChase(); // Ensure direct chasing is off
        // Create a new Queue from the input or set to null if input is null
        this.path = (newPath != null && newPath.Count > 0) ? new Queue<Waypoint>(newPath) : null;

        if (this.path != null)
        {
            pathAnchor = transform.position; // Reset anchor to current position for the new path
            // Optional: Debug draw the new path
            // Debug.Log($"<color=cyan>[AgentMover]</color> {gameObject.name} получил новый путь из {path.Count} точек.");
            // Vector3 previousPoint = transform.position;
            // foreach(var waypoint in path) { if (waypoint != null) Debug.DrawLine(previousPoint, waypoint.transform.position, Color.green, 5f); previousPoint = waypoint.transform.position; }
        } else {
             // If the new path is null or empty, ensure the agent stops
             Stop(); // Calls StopDirectChase and clears path again, resets anchor
        }
    }

    /// <summary>
    /// Checks if the agent is currently considered moving (either following a path or chasing directly).
    /// Does not count as moving while slipping.
    /// </summary>
    /// <returns>True if moving, false otherwise.</returns>
    public bool IsMoving()
    {
        if (isSlipping) return false;

        if (isDirectChasing)
        {
            // Moving if velocity magnitude is significant
            return rb.linearVelocity.magnitude > 0.1f;
        }
        // Moving if currently following a non-empty path
        return path != null && path.Count > 0;
    }


    /// <summary>
    /// Stops all agent movement (path following and direct chasing) and clears the path.
    /// Does not immediately zero out velocity (allows smooth stop via FixedUpdate).
    /// </summary>
    public void Stop()
    {
        StopDirectChase();
        path?.Clear();
        path = null;
        pathAnchor = transform.position; // Reset anchor
        // Don't set rb.velocity = Vector2.zero here; let FixedUpdate handle smooth stopping
        // Debug.Log($"[AgentMover] {gameObject.name} получил команду Stop.");
    }


    // --- Collision Handling for Yielding ---
    /// <summary>
    /// Detects collisions with other agents to initiate yielding behavior.
    /// </summary>
    void OnCollisionStay2D(Collision2D collision)
    {
         // Ignore collisions while yielding or slipping
        if (isYielding || isSlipping) return;

        // Check if the collided object has an AgentMover component
        AgentMover otherMover = collision.gameObject.GetComponent<AgentMover>();
        // Yield only if the other agent exists, is currently moving, and has higher or equal priority (with tie-breaking)
        if (otherMover != null && otherMover.IsMoving())
        {
            // --- Считаем время столкновения для системы "Втягивания живота" ---
            squeezeTimer += Time.deltaTime;
            // -----------------------------------------------------------------
            
            // Yield if other has higher priority OR if priorities are equal and other has a larger InstanceID (arbitrary but consistent tie-breaker)
            if (otherMover.priority > this.priority || (otherMover.priority == this.priority && otherMover.gameObject.GetInstanceID() > this.gameObject.GetInstanceID()))
            {
                StartYielding(); // Start the yielding coroutine
            }
        }
    }

    /// <summary>
    /// Called when collision ends - starts recovery timer for "belly" returning to normal size.
    /// </summary>
    void OnCollisionExit2D(Collision2D collision)
    {
        // If we were colliding with another AgentMover, start recovery timer
        if (collision.gameObject.GetComponent<AgentMover>() != null)
        {
            squeezeRecoveryTimer = 1.0f; // Куоолдаун перед надуванием обратно
        }
    }

    /// <summary>
    /// Starts the YieldRoutine coroutine, stopping any previous one.
    /// </summary>
    private void StartYielding()
    {
        if (yieldingCoroutine != null) StopCoroutine(yieldingCoroutine); // Stop previous yield if any
        yieldingCoroutine = StartCoroutine(YieldRoutine());
    }

    /// <summary>
    /// Coroutine for the yielding behavior (pauses intense path following for a short duration).
    /// </summary>
    private IEnumerator YieldRoutine()
    {
        isYielding = true; // Set yielding flag
        // Debug.Log($"{gameObject.name} начинает уступать."); // Optional log
        yield return new WaitForSeconds(0.5f); // Duration of yielding
        isYielding = false; // Clear yielding flag
        yieldingCoroutine = null; // Clear coroutine handle
        // Debug.Log($"{gameObject.name} перестал уступать."); // Optional log
    }
    // --- End Collision Handling ---


    /// <summary>
    /// Updates the horizontal flip of the character sprite based on velocity.
    /// </summary>
    /// <param name="velocity">The current velocity of the Rigidbody.</param>
    private void UpdateSpriteDirection(Vector2 velocity)
    {
         // Update direction only if renderer exists and not currently slipping/fallen
        if (characterSpriteRenderer != null && !isSlipping)
        {
             // Use a slightly larger threshold to prevent flipping back and forth at very low speeds
            if (velocity.x > 0.15f)
            {
                characterSpriteRenderer.flipX = false; // Moving right, face right
            }
            else if (velocity.x < -0.15f)
            {
                characterSpriteRenderer.flipX = true; // Moving left, face left
            }
            // If horizontal velocity is near zero, keep the current facing direction
        }
    }


    // --- Slip and Recover Logic ---
    /// <summary>
    /// Initiates the slip and recover sequence. Does nothing if already slipping.
    /// </summary>
    public void SlipAndRecover()
    {
        if (isSlipping) return; // Prevent triggering multiple times
        StartCoroutine(SlipAndRecoverRoutine());
    }

    /// <summary>
    /// Coroutine managing the visual and physical states during slipping, falling, lying down, and recovering.
    /// </summary>
    private IEnumerator SlipAndRecoverRoutine()
{
    isSlipping = true;
    Debug.Log($"!!! {gameObject.name} НАЧАЛ СКОЛЬЗИТЬ !!!");

    // --- Toggle Shadow Visibility (hide when slipping) ---
    Transform shadow = transform.Find("VisualsContainer/Shadow");
    if (shadow != null) shadow.gameObject.SetActive(false);

    // --- References and State Saving ---
    DirectorAvatarController director = GetComponent<DirectorAvatarController>(); 
    bool wasUninterruptible = false;
    if (director != null)
    {
        wasUninterruptible = director.IsInUninterruptibleAction;
        director.SetUninterruptible(true); 
    }
    Vector2 lastVelocity = rb.linearVelocity; 
    Stop(); 

    // --- Play Sound ---
    if (fallSound != null && footstepAudioSource != null)
    {
        footstepAudioSource.PlayOneShot(fallSound);
    }

    var clientBrain = GetComponent<ClientPathfinding>();
    
    if (clientBrain != null)
    {
        // Добавляем 10% стресса мгновенно за унижение
        // (Можешь вынести 0.10f в настройки, если захочешь)
        clientBrain.ApplyStressJump(0.10f); 
        Debug.Log($"[AgentMover] {gameObject.name} получил стресс за падение!");
    }

    // --- Fall Physics & Visuals ---
    rb.linearVelocity = Vector2.zero; 
    rb.angularVelocity = 0f;
    rb.bodyType = RigidbodyType2D.Kinematic; 

    Transform characterVisualsTransform = characterSpriteRenderer?.transform.parent; 
    Quaternion originalVisualRotation = characterVisualsTransform != null ? characterVisualsTransform.localRotation : Quaternion.identity;
    float fallDirection = (Random.value > 0.5f) ? 1f : -1f; 
    Quaternion targetVisualRotation = Quaternion.Euler(0, 0, 90f * fallDirection); 

    Vector3 initialRootPosition = transform.position; 
    Vector3 initialPivotWorldPosition = fallPivotPoint != null ? fallPivotPoint.position : initialRootPosition; 

    var visuals = GetComponent<CharacterVisuals>();
    visuals?.SetEmotion(Emotion.Scared); 

    // Animate fall rotation
    float fallDuration = 0.2f;
    for (float t = 0; t < fallDuration && characterVisualsTransform != null; t += Time.deltaTime)
    {
        characterVisualsTransform.localRotation = Quaternion.Slerp(originalVisualRotation, targetVisualRotation, t / fallDuration);
        yield return null;
    }
    if (characterVisualsTransform != null) characterVisualsTransform.localRotation = targetVisualRotation; 

    // Correct root position based on pivot
    if (fallPivotPoint != null)
    {
        Vector3 currentPivotWorldPosition = fallPivotPoint.position; 
        Vector3 correctionVector = initialPivotWorldPosition - currentPivotWorldPosition; 
        transform.position += correctionVector; 
    }
    // --- End Fall Physics & Visuals ---

    // --- Lying Down Phase ---
    ThoughtBubbleController thoughtBubble = GetComponent<ThoughtBubbleController>();
    if (thoughtBubble != null)
    {
        string[] fallComments = { "Ой!", "Ай!", "Упс...", "*Неловкий звук*", "Скользко!", "Вот же ж..." };
        
        // Маленький бонус: если клиент, можно сделать реплику красной (злой)
        Color msgColor = (clientBrain != null) ? Color.red : Color.yellow;
        
        thoughtBubble.ShowPriorityMessage(fallComments[Random.Range(0, fallComments.Length)], 2.0f, msgColor);
    }

    yield return new WaitForSeconds(Random.Range(1.5f, 2.5f)); 
    // --- End Lying Down ---

    // --- Recovery Phase ---
    visuals?.SetEmotion(Emotion.Neutral); 

    // Animate recovery rotation
    float riseDuration = 0.3f;
    Quaternion currentVisualRotation = characterVisualsTransform != null ? characterVisualsTransform.localRotation : targetVisualRotation;
    for (float t = 0; t < riseDuration && characterVisualsTransform != null; t += Time.deltaTime)
    {
        characterVisualsTransform.localRotation = Quaternion.Slerp(currentVisualRotation, originalVisualRotation, t / riseDuration);
        yield return null;
    }
    if (characterVisualsTransform != null) characterVisualsTransform.localRotation = originalVisualRotation; 

    // Snap back to the original root position before the fall
    transform.position = initialRootPosition;

    rb.bodyType = RigidbodyType2D.Dynamic;
    isSlipping = false;

    // --- Toggle Shadow Visibility (re-enable after recovery) ---
    if (shadow != null) shadow.gameObject.SetActive(true);

    // Unblock director actions if they weren't blocked before the fall
    if (director != null && !wasUninterruptible)
    {
        director.SetUninterruptible(false);
    }
      Debug.Log($"{gameObject.name} поднялся после падения.");
    // --- End Recovery ---
}

	public void SetTarget(Vector3 targetPosition)
    {
        // Используем утилиту для построения пути и передаем результат в SetPath
        var calculatedPath = Utilities.PathfindingUtility.BuildPathTo(transform.position, targetPosition, gameObject);
        SetPath(calculatedPath);
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
    }
}