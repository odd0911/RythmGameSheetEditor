using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FMODUnity;
using FMOD.Studio;

public class ChartEditor : MonoBehaviour, IPointerClickHandler
{
    public ScrollRect scrollRect;
    public RectTransform content;
    public GridLayoutGroup gridLayout;
    public GameObject notePrefab;
    public StudioEventEmitter musicEmitter;
    public Button playButton;
    public Button stopButton;
    public float scrollSpeed = 0.1f;
    private float musicLength;
    
    void Start()
    {
        // FMOD에서 음악 길이 가져오기
        musicEmitter.EventInstance.getDescription(out EventDescription eventDescription);
        eventDescription.getLength(out int length);
        musicLength = length;
        
        // 버튼 이벤트 설정
        playButton.onClick.AddListener(PlayMusic);
        stopButton.onClick.AddListener(StopMusic);
    }

    void Update()
    {
        // 음악 진행 시간에 따라 자동 스크롤
        float currentTime;
        musicEmitter.EventInstance.getTimelinePosition(out int position);
        currentTime = position;
        
        float scrollPosition = currentTime / musicLength;
        scrollRect.verticalNormalizedPosition = 1f - scrollPosition;
    }

    public void PlayMusic()
    {
        musicEmitter.EventInstance.start();
    }
    
    public void StopMusic()
    {
        musicEmitter.EventInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // 클릭한 위치를 노트 배치 위치로 변환
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(content, eventData.position, eventData.pressEventCamera, out localPoint);
        
        // 클릭한 위치에 가장 가까운 그리드 셀 찾기
        Vector3 spawnPosition = new Vector3(
            Mathf.Round(localPoint.x / gridLayout.cellSize.x) * gridLayout.cellSize.x,
            Mathf.Round(localPoint.y / gridLayout.cellSize.y) * gridLayout.cellSize.y,
            0);
        
        // 노트 생성
        Instantiate(notePrefab, content.transform).transform.localPosition = spawnPosition;
    }
}
