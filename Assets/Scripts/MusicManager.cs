using UnityEngine;
using UnityEngine.UI; // 버튼을 사용하려면 필요
using FMODUnity;
using TMPro; // TextMeshPro 네임스페이스
using FMOD.Studio;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    private float noteSpeed = 600;

    private enum Mode { Twelve, Sixteen }
    private Mode currentMode = Mode.Sixteen; // 기본 모드: 16분할
    public Button shortNoteButton;
    public Button longNoteButton;

    [SerializeField]
    private EventReference fmodEvent;
    private EventInstance soundInstance;
    public string fmodEventPath; // FMOD 이벤트 경로

    [SerializeField]
    private RectTransform content; // Scroll View의 Content RectTransform

    [SerializeField]
    private GameObject barPrefab; // 매트로놈 바 프리팹

    [SerializeField]
    private GameObject smallLinePrefab; // 작은 라인 프리팹

    [SerializeField]
    private float bpm = 170f; // BPM 값

    [SerializeField]
    private float heightPerTick = 50f; // 틱당 높이 (픽셀)

    [SerializeField]
    private TextMeshProUGUI timeText; // TMP 텍스트 컴포넌트

    [SerializeField]
    private Button button12; // 12분할 버튼
    [SerializeField]
    private Button button16; // 16분할 버튼

    [SerializeField]
    private float heightPerSecond = 100f; // 초당 Content 높이 (비율)
    [SerializeField] 
    private float scrollSpeed = 100f; // 스크롤 속도 조절
    [SerializeField] 
    private GameObject notePrefab; // ➡️ 노트 프리팹 추가
    public GameObject longNotePrefab;
    private GameObject movingNote; // ➡️ 커서를 따라다니는 노트
    private bool isPlacingNote = false; // ➡️ 노트 배치 중인지 여부
    private List<RectTransform> smallBars = new List<RectTransform>();

    // ➡️ 노트 저장용 리스트 및 가로 라인 수 추가
    private List<GameObject> notes = new List<GameObject>();
    private int numberOfLines = 4; // 가로 4줄

    // ➡️ 노트가 배치될 X 좌표 설정
    private float[] lineXPositions = { -75f, -25f, 25f, 75f };

    private int musicLengthMs; // 음악 길이 (밀리초)
    private bool isPlaying = false; // 음악 재생 상태
    private float tickTime; // 틱타임 (초 단위)
    private int pausedTimeMs = 0;
    private Vector2 lastScrollPosition; // 마지막 스크롤 위치 저장
    private List<GameObject> twelveLines = new List<GameObject>();
    private List<GameObject> sixteenLines = new List<GameObject>();
    private bool isLongNoteMode = false;
    private bool isSettingLongNoteEnd = false;
    private Vector2 longNoteStartPos;
    
    
    private void Start()
    {
        // FMOD 이벤트 인스턴스 생성
        soundInstance = RuntimeManager.CreateInstance(fmodEventPath);
        
        tickTime = 60f / bpm;

        // 음악 길이 가져오기
        GetMusicLength();
        GenerateBars();

        // 버튼 이벤트 연결
        button12.onClick.AddListener(SetMode12);
        button16.onClick.AddListener(SetMode16);
    }


    private void UpdateTimeDisplay()
    {
        // 현재 재생 시간 가져오기 (밀리초 단위)
        int currentTimelinePosition;
        soundInstance.getTimelinePosition(out currentTimelinePosition);

        // 밀리초를 분:초:밀리초 형식으로 변환
        string formattedTime = FormatTime(currentTimelinePosition);

        // TMP 텍스트 업데이트
        timeText.text = formattedTime;
    }

    private string FormatTime(int milliseconds)
    {
        int totalSeconds = milliseconds / 1000;
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int ms = milliseconds % 1000;

        // "mm:ss:fff" 형식으로 반환
        return $"{minutes:D2}:{seconds:D2}:{ms:D3}";
    }

    private void GetMusicLength()
    {
        FMOD.Studio.EventDescription eventDescription;
        soundInstance.getDescription(out eventDescription);
        eventDescription.getLength(out musicLengthMs);
        // Content 높이 조정
        AdjustContentHeight();
    }

    private void AdjustContentHeight()
    {
        // 음악 길이를 초 단위로 변환
        float musicLengthSeconds = musicLengthMs / noteSpeed;

        // Content의 높이를 음악 길이에 비례하여 설정
        float newHeight = musicLengthSeconds * heightPerSecond;
        content.sizeDelta = new Vector2(content.sizeDelta.x, newHeight);
    }

    private void GenerateBars()
{
    float tickTimeMs = (60f / bpm) * 1000f;
    int totalTicks = Mathf.CeilToInt(content.rect.height / heightPerTick);
    float startY = 0f;

    // 기존에 생성된 모든 바와 작은 라인 삭제
    foreach (Transform child in content)
    {
        Destroy(child.gameObject);
    }
    twelveLines.Clear();
    sixteenLines.Clear();
    smallBars.Clear();

    // 바 및 작은 라인 생성
    for (int i = 0; i < totalTicks; i++)
    {
        float barPositionY = startY + (i * heightPerTick);
        GameObject bar = Instantiate(barPrefab, content);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchoredPosition = new Vector2(0, barPositionY);
        barRect.localScale = Vector3.one;

        // 12분할 라인 생성 및 비활성화
        for (int j = 1; j <= 12; j++)
        {
            GameObject smallLine = Instantiate(smallLinePrefab, content);
            RectTransform smallLineRect = smallLine.GetComponent<RectTransform>();
            float smallLineY = barPositionY + (j / 12f) * heightPerTick;
            smallLineRect.anchoredPosition = new Vector2(0, smallLineY);
            smallLineRect.localScale = Vector3.one;
            smallLine.SetActive(currentMode == Mode.Twelve); // 초기화할 때 현재 모드에 해당하면 활성화
            twelveLines.Add(smallLine);
            smallBars.Add(smallLineRect);
        }

        // 16분할 라인 생성 및 비활성화
        for (int j = 1; j <= 16; j++)
        {
            GameObject smallLine = Instantiate(smallLinePrefab, content);
            RectTransform smallLineRect = smallLine.GetComponent<RectTransform>();
            float smallLineY = barPositionY + (j / 16f) * heightPerTick;
            smallLineRect.anchoredPosition = new Vector2(0, smallLineY);
            smallLineRect.localScale = Vector3.one;
            smallLine.SetActive(currentMode == Mode.Sixteen); // 초기화할 때 현재 모드에 해당하면 활성화
            sixteenLines.Add(smallLine);
            smallBars.Add(smallLineRect);
        }
    }
}
    


    private void UpdateBarsVisibility()
    {
        // 12분할 활성화 / 비활성화
        foreach (var line in twelveLines)
        {
            line.SetActive(currentMode == Mode.Twelve);
        }

        // 16분할 활성화 / 비활성화
        foreach (var line in sixteenLines)
        {
            line.SetActive(currentMode == Mode.Sixteen);
        }
    }

    public void SetMode12()
    {
        currentMode = Mode.Twelve;
        UpdateBarsVisibility();
    }

    public void SetMode16()
    {
        currentMode = Mode.Sixteen;
        UpdateBarsVisibility();
    }

    private void Update()
{
    if (isPlaying)
    {
        UpdateContentPosition();
        UpdateTimeDisplay();
    }
    else
    {
        // ⏸️ 일시정지 중 사용자가 스크롤을 조작하면 재생 시간 변경
        UpdateTimeFromScroll();
    }
    if (movingNote != null)
        {
            Vector2 mousePosition = Input.mousePosition;
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, mousePosition, null, out localPoint);

            // ➡️ 마우스 위치 그대로 따라다니기
            movingNote.GetComponent<RectTransform>().anchoredPosition = localPoint;
        }

        // 마우스 클릭 처리
        if (Input.GetMouseButtonDown(0))
        {
            if (isLongNoteMode)
            {
                if (!isSettingLongNoteEnd)
                {
                    // ➡️ 롱노트 시작점 고정
                    SnapLongNoteStartToClosestPoint();
                    isSettingLongNoteEnd = true;
                }
                else
                {
                    // ➡️ 롱노트 끝점 고정 및 가장 가까운 작은바에 스냅
                    SnapLongNoteToClosestPoint();
                    movingNote = null;
                    isPlacingNote = false;
                    isSettingLongNoteEnd = false;

                    // ➡️ 다음 노트 미리 생성
                    CreateFollowingNote();
                }
            }
            else
            {
                // ➡️ 숏노트 모드 ➡️ 가장 가까운 작은바에 스냅 후 고정
                SnapNoteToClosestPoint();
                movingNote = null;
                isPlacingNote = false;

                // ➡️ 다음 노트 미리 생성
                CreateFollowingNote();
            }
        }
        if (Input.GetMouseButtonDown(1))
    {
        Vector2 mousePosition = Input.mousePosition;
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, mousePosition, null, out localPoint);

        float closestX = localPoint.x;
        float closestY = localPoint.y;
        float minXDistance = float.MaxValue;
        float minYDistance = float.MaxValue;

        // ➡️ 가장 가까운 X 좌표 찾기
        for (int i = 0; i < lineXPositions.Length; i++)
        {
            float distance = Mathf.Abs(localPoint.x - lineXPositions[i]);
            if (distance < minXDistance)
            {
                minXDistance = distance;
                closestX = lineXPositions[i];
            }
        }

        // ➡️ 가장 가까운 작은바 Y 좌표 찾기
        foreach (RectTransform smallBar in smallBars)
        {
            float distance = Mathf.Abs(localPoint.y - smallBar.anchoredPosition.y);
            if (distance < minYDistance)
            {
                minYDistance = distance;
                closestY = smallBar.anchoredPosition.y;
            }
        }

        // ➡️ 해당 위치에 있는 노트 찾기 및 삭제
        foreach (Transform child in content)
        {
            RectTransform childRect = child.GetComponent<RectTransform>();
            if (childRect != null)
            {
                float xDistance = Mathf.Abs(childRect.anchoredPosition.x - closestX);
                float yDistance = Mathf.Abs(childRect.anchoredPosition.y - closestY);

                // 🔥 X, Y 좌표가 거의 같으면 삭제
                if (xDistance < 5f && yDistance < 5f)
                {
                    Destroy(child.gameObject);
                    isPlacingNote = true;

                    break;
                }
            }
        }
    }
    }

    private void CreateFollowingNote()
    {
        if (isLongNoteMode)
        {
            movingNote = Instantiate(longNotePrefab, content);
        }
        else
        {
            movingNote = Instantiate(notePrefab, content);
        }
        movingNote.GetComponent<RectTransform>().localScale = Vector3.one;
        isPlacingNote = true;
    }

    private void SnapLongNoteStartToClosestPoint()
{
    if (movingNote == null) return;

    RectTransform noteRect = movingNote.GetComponent<RectTransform>();
    Vector2 notePosition = noteRect.anchoredPosition;

    // ➡️ X좌표 범위 확인
    if (notePosition.x > 100 || notePosition.x < -100)
    {
        Destroy(movingNote);
        return;
    }

    // ➡️ 시작점을 가장 가까운 작은바에 스냅
    float closestY = notePosition.y;
    float minYDistance = float.MaxValue;

    foreach (RectTransform smallBar in smallBars)
    {
        float distance = Mathf.Abs(notePosition.y - smallBar.anchoredPosition.y);
        if (distance < minYDistance)
        {
            minYDistance = distance;
            closestY = smallBar.anchoredPosition.y;
        }
    }

    // ➡️ 가장 가까운 스냅 포인트로 이동
    noteRect.anchoredPosition = new Vector2(notePosition.x, closestY);

    // ➡️ 시작점 고정
    longNoteStartPos = noteRect.anchoredPosition;
}

    private void SnapNoteToClosestPoint()
    {
        if (movingNote == null) return;

        RectTransform noteRect = movingNote.GetComponent<RectTransform>();
        Vector2 notePosition = noteRect.anchoredPosition;

        // ➡️ X좌표 범위 확인
        if (notePosition.x > 100 || notePosition.x < -100)
        {
            Destroy(movingNote);
            return;
        }

        float closestX = lineXPositions[0];
    float minXDistance = Mathf.Abs(notePosition.x - lineXPositions[0]);
    for (int i = 1; i < lineXPositions.Length; i++)
    {
        float distance = Mathf.Abs(notePosition.x - lineXPositions[i]);
        if (distance < minXDistance)
        {
            minXDistance = distance;
            closestX = lineXPositions[i];
        }
    }

        // ➡️ 가장 가까운 작은 바 찾기
        float closestY = notePosition.y;
        float minYDistance = float.MaxValue;

        foreach (RectTransform smallBar in smallBars)
        {
            float distance = Mathf.Abs(notePosition.y - smallBar.anchoredPosition.y);
            if (distance < minYDistance)
            {
                minYDistance = distance;
                closestY = smallBar.anchoredPosition.y;
            }
        }

        if (IsOverlappingWithExistingNotes(closestX, closestY))
    {
        Destroy(movingNote);
        movingNote = null;
        isPlacingNote = false;
        return;
    }

        // ➡️ 가장 가까운 스냅 포인트로 이동
        noteRect.anchoredPosition = new Vector2(closestX, closestY);
    }

private void SnapLongNoteToClosestPoint()
    {
        if (movingNote == null) return;

        RectTransform noteRect = movingNote.GetComponent<RectTransform>();
        Vector2 notePosition = noteRect.anchoredPosition;
        Vector2 size = noteRect.sizeDelta;

        // ➡️ X좌표 범위 확인
        if (notePosition.x > 100 || notePosition.x < -100)
        {
            Destroy(movingNote);
            return;
        }

        float closestX = lineXPositions[0];
    float minXDistance = Mathf.Abs(notePosition.x - lineXPositions[0]);
    for (int i = 1; i < lineXPositions.Length; i++)
    {
        float distance = Mathf.Abs(notePosition.x - lineXPositions[i]);
        if (distance < minXDistance)
        {
            minXDistance = distance;
            closestX = lineXPositions[i];
        }
    }

        // ➡️ 가장 가까운 작은 바 찾기 (끝지점 기준)
        float closestY = notePosition.y;
        float minYDistance = float.MaxValue;

        foreach (RectTransform smallBar in smallBars)
        {
            float distance = Mathf.Abs(notePosition.y - smallBar.anchoredPosition.y);
            if (distance < minYDistance)
            {
                minYDistance = distance;
                closestY = smallBar.anchoredPosition.y;
            }
        }

        // ➡️ 시작점과 끝점 계산 후 스냅
        float startY = longNoteStartPos.y;
        float endY = closestY;
        float length = Mathf.Abs(startY - endY);
        Vector2 midPoint = new Vector2(closestX, startY);

        noteRect.anchoredPosition = midPoint;
        noteRect.sizeDelta = new Vector2(noteRect.sizeDelta.x, length);
    }

    private bool IsOverlappingWithExistingNotes(float x, float y)
{
    foreach (Transform child in content)
    {
        RectTransform childRect = child.GetComponent<RectTransform>();
        if (childRect != null)
        {
            Vector2 childPos = childRect.anchoredPosition;
            // ➡️ 같은 위치에 이미 노트가 있다면 겹침으로 판단
            if (Mathf.Abs(childPos.x - x) < 1f && Mathf.Abs(childPos.y - y) < 1f)
            {
                return true;
            }
        }
    }
    return false;
}

    public void SetShortNoteMode()
    {
        isLongNoteMode = false;
        if (movingNote != null)
        {
            Destroy(movingNote);
        }
        CreateFollowingNote();
    }

    public void SetLongNoteMode()
    {
        isLongNoteMode = true;
        if (movingNote != null)
        {
            Destroy(movingNote);
        }
        CreateFollowingNote();
    }

// 스크롤 위치를 기반으로 재생 시간을 변경하는 함수
private void UpdateTimeFromScroll()
{
    if (!isPlaying) // 일시정지 상태에서만 실행
    {
        float scrollInput = Input.mouseScrollDelta.y; // 마우스 스크롤 입력

        if (Mathf.Abs(scrollInput) > 0.1f) // 스크롤 입력이 있으면 실행
        {
            // 스크롤 방향에 따라 시간 변경
            pausedTimeMs += Mathf.RoundToInt(scrollInput * scrollSpeed);
            pausedTimeMs = Mathf.Clamp(pausedTimeMs, 0, musicLengthMs); // 범위 제한

            // 변경된 시간에 맞춰 Content 위치 조정
            float newYPosition = -(pausedTimeMs / 600f) * heightPerSecond;
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, newYPosition);

            // UI에 업데이트된 시간 표시
            timeText.text = FormatTime(pausedTimeMs);
            soundInstance.setTimelinePosition(pausedTimeMs);
        }
    }
}

    private void UpdateContentPosition()
    {
        // 현재 재생 시간 가져오기
        int currentTimelinePosition;
        soundInstance.getTimelinePosition(out currentTimelinePosition);

        // 재생된 시간(초)에 비례하여 Content 위치 업데이트
        float elapsedTimeSeconds = currentTimelinePosition / 600f;
        float newYPosition = elapsedTimeSeconds * heightPerSecond;//현재 content위치(-newPosition) = 현재 시간 * 시간비례높이상수 / 600

        // Content 위치 설정 (아래로 이동)
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, -newYPosition);
    }

    
public void PlaySound()
{
    FMOD.Studio.PLAYBACK_STATE playbackState;
    soundInstance.getPlaybackState(out playbackState);

    bool isPaused;
    soundInstance.getPaused(out isPaused);

    if (playbackState == FMOD.Studio.PLAYBACK_STATE.STOPPED)
    {
        soundInstance.start();
        isPlaying = true;
    }
    else if (isPaused)
    {
        soundInstance.setPaused(false);
        isPlaying = true;
    }
}

public void PauseSound()
{
    bool isPaused;
    soundInstance.getPaused(out isPaused);

    if (!isPaused)
    {
        // 현재 재생 시간을 저장
        soundInstance.getTimelinePosition(out pausedTimeMs);
        soundInstance.setPaused(true);
        soundInstance.setTimelinePosition(pausedTimeMs);
        isPlaying = false;
    }
}


    public void StopSound()
{
    soundInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); // 즉시 정지
    soundInstance.setTimelinePosition(0); // 타임라인 위치 초기화

    // FMOD 상태를 완전히 초기화하기 위해 새 인스턴스 생성
    soundInstance.release();
    soundInstance = RuntimeManager.CreateInstance(fmodEventPath);

    // 스크롤도 맨 처음 위치로 이동
    content.anchoredPosition = new Vector2(content.anchoredPosition.x, 0);
    isPlaying = false;
    pausedTimeMs = 0; // 저장된 시간 초기화

    // UI의 재생 시간도 00:00:000으로 초기화
    timeText.text = FormatTime(0);
}


    private void OnDestroy()
    {
        soundInstance.release();
    }
    
}
