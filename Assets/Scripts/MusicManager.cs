using UnityEngine;
using FMODUnity;
using TMPro; // TextMeshPro 네임스페이스

public class MusicManager : MonoBehaviour
{
    private float noteSpeed = 600;

    [SerializeField]
    [EventRef] 
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
    private float heightPerSecond = 100f; // 초당 Content 높이 (비율)
    private int musicLengthMs; // 음악 길이 (밀리초)
    private bool isPlaying = false; // 음악 재생 상태
    private float tickTime; // 틱타임 (초 단위)
    
    private void Start()
    {
        // FMOD 이벤트 인스턴스 생성
        soundInstance = RuntimeManager.CreateInstance(fmodEventPath);
        
        tickTime = 60f / bpm;

        // 음악 길이 가져오기
        GetMusicLength();
        GenerateBars();
    }

    private FMOD.Studio.EventInstance soundInstance;

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
        // 틱 타임 계산 (밀리초 단위)
        float tickTimeMs = (60f / bpm) * 1000f;

        // 총 틱 수 계산 (Content 높이 기반)
        int totalTicks = Mathf.CeilToInt(content.rect.height / heightPerTick);

        // Content의 시작 위치 (맨 아래)
        float startY = -(content.rect.height/2);

        // 매트로놈 바 생성
        for (int i = 0; i < totalTicks; i++)
        {
            float barPositionY = startY + (i * heightPerTick);

            // 프리팹 생성 및 설정
            GameObject bar = Instantiate(barPrefab, content);
            RectTransform barRect = bar.GetComponent<RectTransform>();
            barRect.anchoredPosition = new Vector2(0, barPositionY);
            barRect.localScale = Vector3.one;
            // 작은 라인 추가 (12개)
        for (int j = 1; j <= 12; j++)
        {
            GameObject smallLine = Instantiate(smallLinePrefab, bar.transform);
            RectTransform smallLineRect = smallLine.GetComponent<RectTransform>();

            // 12개의 작은 라인을 균등한 간격으로 배치
            float smallLineY = (j / 12f) * heightPerTick - (heightPerTick / 2);
            smallLineRect.anchoredPosition = new Vector2(0, smallLineY+125);
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
    }

    private void UpdateContentPosition()
    {
        // 현재 재생 시간 가져오기
        int currentTimelinePosition;
        soundInstance.getTimelinePosition(out currentTimelinePosition);

        // 재생된 시간(초)에 비례하여 Content 위치 업데이트
        float elapsedTimeSeconds = currentTimelinePosition / 600f;
        float newYPosition = elapsedTimeSeconds * heightPerSecond;

        // Content 위치 설정 (아래로 이동)
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, -newYPosition-100);
    }

    public void PlaySound()
    {
        if (!isPlaying)
        {
            soundInstance.start();
            isPlaying = true;
        }
    }

    public void StopSound()
    {
        soundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
        isPlaying = false;
    }

    private void OnDestroy()
    {
        soundInstance.release();
    }
}
