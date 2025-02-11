using UnityEngine;
using UnityEngine.UI; // 버튼을 사용하려면 필요
using FMODUnity;
using TMPro; // TextMeshPro 네임스페이스
using FMOD.Studio;

public class MusicManager : MonoBehaviour
{
    private float noteSpeed = 600;

    private enum Mode { Twelve, Sixteen }
    private Mode currentMode = Mode.Sixteen; // 기본 모드: 16분할

    [SerializeField]
    private EventReference fmodEvent;
    private EventInstance soundInstance;
    private string fmodEventPath = "event:/music_test1"; // FMOD 이벤트 경로

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
    private int musicLengthMs; // 음악 길이 (밀리초)
    private bool isPlaying = false; // 음악 재생 상태
    private float tickTime; // 틱타임 (초 단위)
    private int pausedTimeMs = 0;
    private Vector2 lastScrollPosition; // 마지막 스크롤 위치 저장
    
    
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
        float startY = -(content.rect.height / 2);

        foreach (Transform child in content)
        {
            Destroy(child.gameObject); // 기존 라인 삭제
        }

        for (int i = 0; i < totalTicks; i++)
        {
            float barPositionY = startY + (i * heightPerTick);
            GameObject bar = Instantiate(barPrefab, content);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchoredPosition = new Vector2(0, barPositionY);
            barRect.localScale = Vector3.one;

            int subdivisions = (currentMode == Mode.Twelve) ? 12 : 16;
            for (int j = 1; j <= subdivisions; j++)
            {
                GameObject smallLine = Instantiate(smallLinePrefab, content);
                RectTransform smallLineRect = smallLine.GetComponent<RectTransform>();
                float smallLineY = barPositionY + (j / (float)subdivisions) * heightPerTick;
                smallLineRect.anchoredPosition = new Vector2(0, smallLineY);
                smallLineRect.localScale = Vector3.one;
            }
        }
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
            float newYPosition = -(pausedTimeMs / 1000f) * heightPerSecond;
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

    public void SetMode12()
    {
        currentMode = Mode.Twelve;
        GenerateBars();
    }

    public void SetMode16()
    {
        currentMode = Mode.Sixteen;
        GenerateBars();
    }


    private void OnDestroy()
    {
        soundInstance.release();
    }
    
}
