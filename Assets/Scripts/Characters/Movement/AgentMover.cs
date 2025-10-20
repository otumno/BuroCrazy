// Файл: AgentMover.cs - ПОЛНАЯ ИСПРАВЛЕННАЯ ВЕРСИЯ
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
public class AgentMover : MonoBehaviour
{
    [Header("Параметры Движения")]
    public float moveSpeed = 2f;
    public float stoppingDistance = 0.2f;

    [Header("Простая анимация ходьбы")]
    public SpriteRenderer characterSpriteRenderer;
    public Sprite idleSprite;
    public Sprite walkSprite1;
    public Sprite walkSprite2;
    public float animationSpeed = 0.3f;
    private float animationTimer = 0f;
    private bool isFirstWalkSprite = true;
	
	[Header("Звуки шагов")]
    [Tooltip("Список из 6 (или больше) звуков шагов для случайного выбора")]
    public List<AudioClip> footstepSounds;
	
	[Header("Настройки Падения")]
	[Tooltip("Точка опоры при падении (между ног на уровне пола)")]
	public Transform fallPivotPoint;
	[Tooltip("Звук, проигрываемый при падении")]
	public AudioClip fallSound;

    [Tooltip("Компонент AudioSource для проигрывания шагов. Если не указан, будет найден или добавлен автоматически.")]
    public AudioSource footstepAudioSource;

    [Tooltip("Минимальная скорость, при которой воспроизводятся шаги")]
    public float minSpeedForFootsteps = 0.5f;

    [Tooltip("Примерное расстояние между шагами при базовой скорости (moveSpeed). Чем меньше, тем чаще шаги.")]
    public float distanceBetweenSteps = 0.8f; // Метры

    // Внутренние переменные для отслеживания шагов
    private float distanceCoveredSinceLastStep = 0f;
    private Vector3 lastPositionForSteps;

    // --- ДОБАВЛЕНО: ДИНАМИЧЕСКИЙ СВЕТ ---
    [Header("Динамический свет")]
    [Tooltip("Перетащите сюда Transform объекта Spotlight")]
    [SerializeField] private Transform dynamicLight; 
    [Tooltip("На какое расстояние свет будет смещаться вперед при движении")]
    [SerializeField] public float lightForwardOffset = 0.5f;
    [Tooltip("Насколько плавно свет возвращается на место и следует за движением")]
    [SerializeField] private float lightSmoothing = 5f;
    
    // --- ВОЗВРАЩЕНО: Недостающие поля из вашей версии ---
    private float baseMoveSpeed;
    public UnityEngine.AI.NavMeshAgent agent;
    [Header("Система 'Резиночки'")]
    [Tooltip("Насколько сильно персонаж стремится вернуться на свой путь.")]
    public float rubberBandStrength = 5f;
    [Tooltip("Приоритет персонажа. Решает, кто кого продавливает.")]
    public int priority = 1;
    // ---------------------------------------------------

    private Rigidbody2D rb;
    private Queue<Waypoint> path;
    private Vector2 pathAnchor; 
    private bool isYielding = false;
    private Coroutine yieldingCoroutine;
    private float dirtTimer = 0f;
    private float dirtInterval = 0.25f;

    private bool isDirectChasing = false;
    private Vector2 directChaseTarget;
    
    [Header("Настройки преследования")]
    [Tooltip("С какого расстояния персонаж начнет замедляться при прямой погоне.")]
    public float slowingDistance = 2.0f;
    [Tooltip("Насколько плавно персонаж меняет скорость.")]
    public float movementSmoothing = 5f;
	
	public bool IsSlipping => isSlipping;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        pathAnchor = transform.position;
        baseMoveSpeed = moveSpeed;
        lastPositionForSteps = transform.position; // Инициализируем позицию для шагов

        // --- ИЗМЕНЕНИЕ НАЧАЛО: Получаем или добавляем AudioSource ---
        if (footstepAudioSource == null)
        {
            footstepAudioSource = GetComponent<AudioSource>();
            if (footstepAudioSource == null)
            {
                footstepAudioSource = gameObject.AddComponent<AudioSource>();
                footstepAudioSource.playOnAwake = false;
                footstepAudioSource.spatialBlend = 1.0f; // 3D Sound
                footstepAudioSource.volume = 0.4f; // Громкость по умолчанию
                Debug.LogWarning($"AudioSource для шагов добавлен автоматически к {gameObject.name}. Настройте его параметры при необходимости.");
            }
        }
        // --- ИЗМЕНЕНИЕ КОНЕЦ ---
    }
	
	

	
	private bool isSlipping = false;
	
	public void SlipAndRecover()
{
    if (isSlipping) return; // Если уже падаем, ничего не делаем
    StartCoroutine(SlipAndRecoverRoutine());
}

private IEnumerator SlipAndRecoverRoutine()
    {
        isSlipping = true;
        Debug.Log($"!!! {gameObject.name} НАЧАЛ СКОЛЬЗИТЬ !!!");

        DirectorAvatarController director = GetComponent<DirectorAvatarController>();
        bool wasUninterruptible = false; // Default value

        // --- Используем метод SetUninterruptible ---
        if (director != null)
        {
            wasUninterruptible = director.IsInUninterruptibleAction; // Read the value
            director.SetUninterruptible(true); // Call the public method to set the flag
        }
        // ---

        // --- Воспроизводим звук падения ---
        if (fallSound != null && footstepAudioSource != null)
        {
            footstepAudioSource.PlayOneShot(fallSound);
        }
        // ---

        // --- Фаза Падения ---
        Vector2 lastVelocity = rb.linearVelocity;
        Stop();
        rb.linearDamping = 5f;

        Transform characterVisualsTransform = characterSpriteRenderer?.transform.parent;
        Quaternion originalVisualRotation = characterVisualsTransform != null ? characterVisualsTransform.localRotation : Quaternion.identity;
        float fallDirection = (Random.value > 0.5f) ? 1f : -1f;
        Quaternion targetVisualRotation = Quaternion.Euler(0, 0, 90f * fallDirection);

        Vector3 initialRootPosition = transform.position;
        Vector3 initialPivotWorldPosition = initialRootPosition;
        if (fallPivotPoint != null) { initialPivotWorldPosition = fallPivotPoint.position; }
        else { Debug.LogWarning($"FallPivotPoint не назначен для {gameObject.name}!"); }

        var visuals = GetComponent<CharacterVisuals>();
        visuals?.SetEmotion(Emotion.Scared);

        // Анимация падения (вращение)
        float fallDuration = 0.2f;
        for (float t = 0; t < fallDuration && characterVisualsTransform != null; t += Time.deltaTime)
        {
            characterVisualsTransform.localRotation = Quaternion.Slerp(originalVisualRotation, targetVisualRotation, t / fallDuration);
            yield return null;
        }
        if (characterVisualsTransform != null) characterVisualsTransform.localRotation = targetVisualRotation;

        // Коррекция позиции корня
        if (fallPivotPoint != null)
        {
            Vector3 currentPivotWorldPosition = fallPivotPoint.position;
            Vector3 correctionVector = initialPivotWorldPosition - currentPivotWorldPosition;
            transform.position += correctionVector;
        }
        // --- Конец Фазы Падения ---

        // --- Фаза "Лежим на полу" ---
        ThoughtBubbleController thoughtBubble = GetComponent<ThoughtBubbleController>();
        if (thoughtBubble != null)
        {
            string[] fallComments = { "Ой!", "Ай!", "Упс...", "*Неловкий звук*", "Скользко!", "Вот же ж..." };
            thoughtBubble.ShowPriorityMessage(fallComments[Random.Range(0, fallComments.Length)], 2.0f, Color.yellow);
        }

        yield return new WaitForSeconds(Random.Range(1.5f, 2.5f)); // Лежим
        // --- Конец Фазы "Лежим на полу" ---

        // --- Фаза Подъема ---
        rb.linearDamping = 0f; // Возвращаем нормальное трение
        visuals?.SetEmotion(Emotion.Neutral);

        Vector3 fallenRootPosition = transform.position; // Позиция перед началом подъема

        // Анимация подъема (обратное вращение)
        float riseDuration = 0.3f;
        Quaternion currentVisualRotation = characterVisualsTransform != null ? characterVisualsTransform.localRotation : targetVisualRotation;
        for (float t = 0; t < riseDuration && characterVisualsTransform != null; t += Time.deltaTime)
        {
            characterVisualsTransform.localRotation = Quaternion.Slerp(currentVisualRotation, originalVisualRotation, t / riseDuration);
            yield return null;
        }
        if (characterVisualsTransform != null) characterVisualsTransform.localRotation = originalVisualRotation;

        // Возвращаем корень на исходную позицию резко
        transform.position = initialRootPosition;
        // --- Конец Фазы Подъема ---

        // --- Завершение ---
        isSlipping = false;

        // --- Используем метод SetUninterruptible для снятия блокировки ---
        // Снимаем блокировку, только если директор существует И он НЕ был занят до падения
        if (director != null && !wasUninterruptible)
        {
            director.SetUninterruptible(false); // Call the public method to clear the flag
        }
        // ---

        // Debug.Log($"{gameObject.name} поднялся."); // Optional log
        // RePath(); // Optional: Recalculate path if needed
    }
	
    public void ApplySpeedMultiplier(float multiplier)
    {
        moveSpeed = baseMoveSpeed * multiplier;
    }

	void Update()
    {
        HandleFootsteps(); // Обработка логики шагов

        // Анимацию и свет тоже можно оставить здесь, если они не строго привязаны к физике
        HandleWalkAnimation();
        UpdateDynamicLightPosition();
    }

    void FixedUpdate()
    {
        Vector2 desiredVelocity;
        if (isDirectChasing)
        {
            Vector2 vectorToTarget = directChaseTarget - (Vector2)transform.position;
            float distanceToTarget = vectorToTarget.magnitude;
            
            float targetSpeed = moveSpeed;
            if (distanceToTarget < slowingDistance)
            {
                targetSpeed = moveSpeed * (distanceToTarget / slowingDistance);
            }
            desiredVelocity = (distanceToTarget > 0.01f) ? vectorToTarget.normalized * targetSpeed : Vector2.zero;
        }
        else if (path == null || path.Count == 0)
        {
            desiredVelocity = Vector2.zero;
        }
        else
        {
            Waypoint targetWaypoint = path.Peek();
            pathAnchor = Vector2.MoveTowards(pathAnchor, targetWaypoint.transform.position, moveSpeed * Time.fixedDeltaTime);
            float currentStrength = isYielding ? rubberBandStrength / 4f : rubberBandStrength;
            desiredVelocity = (pathAnchor - (Vector2)transform.position) * currentStrength;
            desiredVelocity = Vector2.ClampMagnitude(desiredVelocity, moveSpeed * 2f);

            if (Vector2.Distance(transform.position, targetWaypoint.transform.position) < stoppingDistance)
            {
                pathAnchor = targetWaypoint.transform.position;
                path.Dequeue();
            }
        }

        rb.linearVelocity = Vector2.Lerp(rb.linearVelocity, desiredVelocity, Time.fixedDeltaTime * movementSmoothing);
        UpdateSpriteDirection(rb.linearVelocity);
        HandleDirtLogic();
        HandleWalkAnimation();
        UpdateDynamicLightPosition();
    }

	private void HandleFootsteps()
    {
        // Рассчитываем дистанцию, пройденную с прошлого кадра
        float distanceMoved = Vector3.Distance(transform.position, lastPositionForSteps);
        distanceCoveredSinceLastStep += distanceMoved;
        lastPositionForSteps = transform.position; // Обновляем позицию

        // Получаем текущую скорость из Rigidbody
        float currentSpeed = rb.linearVelocity.magnitude;

        // Проверяем, двигаемся ли мы достаточно быстро и есть ли звуки/источник
        if (currentSpeed > minSpeedForFootsteps && footstepSounds != null && footstepSounds.Count > 0 && footstepAudioSource != null)
        {
            // Рассчитываем необходимое расстояние между шагами для текущей скорости
            // Чем выше скорость относительно базовой, тем меньше расстояние (чаще шаги)
            float speedRatio = Mathf.Max(0.1f, currentSpeed / baseMoveSpeed); // Отношение текущей скорости к базовой (с ограничением)
            float requiredDistance = distanceBetweenSteps / speedRatio;

            // Если пройдено достаточное расстояние
            if (distanceCoveredSinceLastStep >= requiredDistance)
            {
                PlayFootstepSound(); // Проигрываем звук шага
                distanceCoveredSinceLastStep -= requiredDistance; // Вычитаем "потраченное" расстояние (позволяет сохранить остаток для следующего шага)
                // Или можно просто сбросить: distanceCoveredSinceLastStep = 0f;
            }
        }
        else
        {
            // Если стоим или движемся медленно, сбрасываем счетчик пройденного расстояния
             if (currentSpeed < 0.1f) // Добавим небольшой порог для сброса
                 distanceCoveredSinceLastStep = 0f;
        }
    }

    // Метод для проигрывания случайного звука шага
    private void PlayFootstepSound()
    {
        // Дополнительные проверки на всякий случай
        if (footstepSounds == null || footstepSounds.Count == 0 || footstepAudioSource == null) return;

        // Выбираем случайный индекс из списка звуков
        int randomIndex = Random.Range(0, footstepSounds.Count);
        AudioClip clipToPlay = footstepSounds[randomIndex];

        // Проигрываем выбранный звук, если он не null
        if (clipToPlay != null)
        {
            // PlayOneShot позволяет звукам шагов немного накладываться, если персонаж бежит быстро
            footstepAudioSource.PlayOneShot(clipToPlay);
        }
    }

    private void UpdateDynamicLightPosition()
    {
        if (dynamicLight == null) return;

        Vector2 targetPosition;
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            targetPosition = rb.linearVelocity.normalized * lightForwardOffset;
        }
        else
        {
            targetPosition = Vector2.zero;
        }
        
        dynamicLight.localPosition = Vector2.Lerp(dynamicLight.localPosition, targetPosition, Time.fixedDeltaTime * lightSmoothing);
    }
    
    private void HandleWalkAnimation()
    {
        if (characterSpriteRenderer == null || idleSprite == null || walkSprite1 == null || walkSprite2 == null) return;

        if (rb.linearVelocity.magnitude > 0.1f)
        {
            animationTimer += Time.fixedDeltaTime;
            if (animationTimer >= animationSpeed)
            {
                animationTimer = 0f;
                isFirstWalkSprite = !isFirstWalkSprite;
                characterSpriteRenderer.sprite = isFirstWalkSprite ? walkSprite1 : walkSprite2;
            }
        }
        else
        {
            characterSpriteRenderer.sprite = idleSprite;
            animationTimer = 0f;
        }
    }

    public void SetAnimationSprites(Sprite idle, Sprite walk1, Sprite walk2)
    {
        this.idleSprite = idle;
        this.walkSprite1 = walk1;
        this.walkSprite2 = walk2;
    }

    // --- ВОЗВРАЩЕНЫ: Недостающие методы из вашей версии ---
    public void StartDirectChase(Vector2 targetPosition)
    {
        isDirectChasing = true;
        directChaseTarget = targetPosition;
        path?.Clear();
    }

    public void UpdateDirectChase(Vector2 targetPosition)
    {
        if (isDirectChasing)
        {
            directChaseTarget = targetPosition;
        }
    }

    public void StopDirectChase()
    {
        isDirectChasing = false;
    }
    // ------------------------------------------------------
    
    private void HandleDirtLogic()
    {
        if (rb.linearVelocity.magnitude > 0.1f)
        {
            dirtTimer += Time.fixedDeltaTime;
            if (dirtTimer >= dirtInterval)
            {
                dirtTimer = 0f;
                DirtGridManager.Instance?.AddTraffic(transform.position);
            }
        }
    }

    public void SetPath(Queue<Waypoint> newPath)
    {
        isDirectChasing = false;
        this.path = newPath;
        if (this.path != null && this.path.Count > 0)
        {
            pathAnchor = transform.position;
            Debug.Log($"<color=cyan>[AgentMover]</color> {gameObject.name} получил новый путь из {path.Count} точек.");
            Vector3 previousPoint = transform.position;
            foreach(var waypoint in path)
            {
                Debug.DrawLine(previousPoint, waypoint.transform.position, Color.green, 10f);
                previousPoint = waypoint.transform.position;
            }
        }
    }

    public bool IsMoving()
    {
        if (isDirectChasing)
        {
            return rb.linearVelocity.magnitude > 0.1f;
        }
        return path != null && path.Count > 0;
    }

    public void Stop()
    {
        StopDirectChase();
        path?.Clear();
        pathAnchor = transform.position;
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (isYielding) return;
        AgentMover otherMover = collision.gameObject.GetComponent<AgentMover>();
        if (otherMover != null)
        {
            if (otherMover.priority > this.priority)
            {
                StartYielding();
            }
            else if (otherMover.priority == this.priority)
            {
                if(this.gameObject.GetInstanceID() > collision.gameObject.GetInstanceID())
                {
                    StartYielding();
                }
            }
        }
    }

    private void StartYielding()
    {
        if (yieldingCoroutine != null) StopCoroutine(yieldingCoroutine);
        yieldingCoroutine = StartCoroutine(YieldRoutine());
    }

    private IEnumerator YieldRoutine()
    {
        isYielding = true;
        yield return new WaitForSeconds(0.5f);
        isYielding = false;
        yieldingCoroutine = null;
    }
    
    private void UpdateSpriteDirection(Vector2 velocity)
    {
        if (characterSpriteRenderer != null)
        {
            if (velocity.x > 0.1f) characterSpriteRenderer.flipX = false;
            else if (velocity.x < -0.1f) characterSpriteRenderer.flipX = true;
        }
    }
}