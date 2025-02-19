using UnityEngine;
using UnityEngine.UI; // 버튼을 사용하려면 필요
using FMODUnity;
using TMPro; // TextMeshPro 네임스페이스
using FMOD.Studio;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System;

public class MusicManager : MonoBehaviour
{
    private enum Mode { Twelve, Sixteen }
    private Mode currentMode = Mode.Sixteen; // 기본 모드: 16분할
    List<string> songNames = new List<string> {"Usagi_Flap","Summer_Attack!","Tok9_Train"};
    public Dropdown songDropdown;  


    [SerializeField]
    private EventInstance soundInstance;
    public string fmodEventPath; // FMOD 이벤트 경로

    [SerializeField]
    private RectTransform content; // Scroll View의 Content RectTransform

    [SerializeField]
    private GameObject barPrefab; // 매트로놈 바 프리팹

    [SerializeField]
    private GameObject smallLinePrefab; // 작은 라인 프리팹

    [SerializeField]
    private List<GameObject> allNotes; // 모든 노트를 담을 리스트
    private float longNoteHeightThreshold = 100f; // 롱노트를 판별하는 높이 기준
    private string title = "Usagi_Flap";
    private float offset = 0f; // Offset 값
    private float bpm = 170f; // BPM 값
    List<string> noteData = new List<string>();

    [SerializeField]
    private TextMeshProUGUI timeText; // TMP 텍스트 컴포넌트

    [SerializeField]
    private Button button12; // 12분할 버튼

    [SerializeField]
    private Button button16; // 16분할 버튼

    [SerializeField] 
    private float scrollSpeed = 100f; // 스크롤 속도 조절
    [SerializeField] 
    public GameObject notePrefab; // ➡️ 노트 프리팹 추가
    public GameObject longNotePrefab;
    private GameObject movingNote; // ➡️ 커서를 따라다니는 노트
    private List<RectTransform> smallBars = new List<RectTransform>();

    // ➡️ 노트가 배치될 X 좌표 설정
    private float[] lineXPositions = { -75f, -25f, 25f, 75f };

    private int musicLengthMs; // 음악 길이 (밀리초)
    private bool isPlaying = false; // 음악 재생 상태
    private float tickTimeMs; // 틱타임 (초 단위)
    private float offsetHeight = 0;
    private int pausedTimeMs = 0;
    private List<GameObject> twelveLines = new List<GameObject>();
    private List<GameObject> sixteenLines = new List<GameObject>();
    private bool isLongNoteMode = false;
    private bool isSettingLongNoteEnd = false;
    private Vector2 longNoteStartPos;
    
    
    private void Start()
    {
        // FMOD 이벤트 인스턴스 생성
        fmodEventPath = $"event:/{title}";
        soundInstance = RuntimeManager.CreateInstance(fmodEventPath);
        
        tickTimeMs = 60f / bpm *1000;

        // 음악 길이 가져오기
        GetMusicLength();

        SetupDropdown();

        // 버튼 이벤트 연결
        button12.onClick.AddListener(SetMode12);
        button16.onClick.AddListener(SetMode16);
        LoadNoteSheet();
        GenerateBars();
        PlaceSheetNotes();
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

        // Content의 높이를 음악 길이에 비례하여 설정
        float newHeight = musicLengthMs;
        content.sizeDelta = new Vector2(content.sizeDelta.x, newHeight/2);
    }

    void SetupDropdown()
    {
        // 🎵 드롭다운 초기화 및 리스트 추가
        songDropdown.ClearOptions();
        songDropdown.AddOptions(songNames);

        // 🎵 기본 선택값
        songDropdown.value = 0;

        // 🎵 드롭다운 변경 시 이벤트 연결
        songDropdown.onValueChanged.AddListener(delegate {
            OnSongSelected(songDropdown.value);
        });
    }

    void OnSongSelected(int index)
    {
        string selectedSong = songNames[index];
        Debug.Log($"선택한 노래: {selectedSong}");

        // 🎵 시트 교체
        SelectSong(selectedSong);
    }

    void SelectSong(string songName)
    {
        title = songName;
        fmodEventPath = $"event:/{title}";
        soundInstance = RuntimeManager.CreateInstance(fmodEventPath);
        GetMusicLength();
        ClearAllNotes();
    }
    void ClearAllNotes()
    {
        foreach (Transform child in content)
        {
            DestroyImmediate(child.gameObject);
        }

        allNotes = new List<GameObject>(); // 리스트를 새롭게 할당
        noteData.Clear();

        Debug.Log("기존 노트를 모두 삭제했습니다.");
        StartCoroutine(DelayedReload()); // 한 프레임 뒤 실행
    }

    IEnumerator DelayedReload()
    {
        yield return new WaitForEndOfFrame(); // 한 프레임 대기
        LoadNoteSheet();
        GenerateBars();
        PlaceSheetNotes();
    }

    private void GenerateBars()
{
    int totalTicks = Mathf.CeilToInt(musicLengthMs / tickTimeMs);

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
        float barPositionY = i * tickTimeMs;
        GameObject bar = Instantiate(barPrefab, content);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchoredPosition = new Vector2(0, offsetHeight + barPositionY/2);
        barRect.localScale = Vector3.one;

        // 12분할 라인 생성 및 비활성화
        for (int j = 1; j <= 6; j++)
        {
            GameObject smallLine = Instantiate(smallLinePrefab, content);
            RectTransform smallLineRect = smallLine.GetComponent<RectTransform>();
            float smallLineY = barPositionY + (j / 6f) * tickTimeMs;
            smallLineRect.anchoredPosition = new Vector2(0, offsetHeight +smallLineY/2);
            smallLineRect.localScale = Vector3.one;
            smallLine.SetActive(currentMode == Mode.Twelve); // 초기화할 때 현재 모드에 해당하면 활성화
            twelveLines.Add(smallLine);
            smallBars.Add(smallLineRect);
        }

        // 16분할 라인 생성 및 비활성화
        for (int j = 1; j <= 8; j++)
        {
            GameObject smallLine = Instantiate(smallLinePrefab, content);
            RectTransform smallLineRect = smallLine.GetComponent<RectTransform>();
            float smallLineY = barPositionY + (j / 8f) * tickTimeMs;
            smallLineRect.anchoredPosition = new Vector2(0, offsetHeight +smallLineY/2);
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

    void LoadNoteSheet()
    {
        // ➡️ 저장된 txt 파일 경로
        string path = Application.persistentDataPath + $"/{title}.txt";
        string BPM = "170";
        if (title == "Tok9_Train")
        {
            BPM = "159";
        }


        // ➡️ 파일이 없으면 기본 파일 생성
    if (!File.Exists(path))
    {
        Debug.LogWarning("노트 시트 파일이 없어 기본 파일을 생성합니다.");

        // ➡️ 기본 노트 시트 내용 작성
        List<string> Usagi_Flap = new List<string>
        {
            "[Description]",
            $"Title: {title}",
            "",
            "[Audio]",
            $"BPM: {BPM}",
            "Offset: 0",
            "",
            "[Note]"
        };

        // ➡️ txt 파일로 저장
        File.WriteAllLines(path, Usagi_Flap);
        Debug.Log($"기본 노트 시트를 생성했습니다: {path}");
    }

        // ➡️ txt 파일 읽기
        string[] lines = File.ReadAllLines(path);

        // ➡️ 데이터 파싱
        bool isNoteSection = false;
        foreach (string line in lines)
        {
            if (line.StartsWith("Title:"))
            {
                title = line.Replace("Title:", "").Trim();
            }
            else if (line.StartsWith("BPM:"))
            {
                bpm = int.Parse(line.Replace("BPM:", "").Trim());
            }
            else if (line.StartsWith("Offset:"))
            {
                offset = int.Parse(line.Replace("Offset:", "").Trim());
                offsetHeight = offset/2;
            }
            else if (line.StartsWith("[Note]"))
            {
                isNoteSection = true;
            }
            else if (isNoteSection && !string.IsNullOrWhiteSpace(line))
            {
                noteData.Add(line.Trim());
            }
        }

        Debug.Log($"노트 시트 불러오기 완료! Title: {title}, BPM: {bpm}, Offset: {offset}");
    }

    void PlaceSheetNotes()
    {
        Debug.Log(notePrefab == null ? "notePrefab is NULL" : "notePrefab is OK");
        foreach (string note in noteData)
        {
            string[] parts = note.Split(',');

            float time = float.Parse(parts[0].Trim());
            int type = int.Parse(parts[1].Trim());
            int lane = int.Parse(parts[2].Trim());

            float xPos = 0;
            if (lane == 1) xPos = -75;
            else if (lane == 2) xPos = -25;
            else if (lane == 3) xPos = 25;
            else if (lane == 4) xPos = 75;

            float yPos = time/2;

            if (type == 0)
            {
                // ➡️ 숏노트 배치
                GameObject noteObject = Instantiate(notePrefab, content);
                noteObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(xPos, yPos);
                allNotes.Add(noteObject);
                Debug.Log("노트 배치!");
            }
            else if (type == 1)
            {
                // ➡️ 롱노트 배치
                float endTime = float.Parse(parts[3].Trim());
                float endYPos = endTime / 2;

                GameObject longNoteObject = Instantiate(longNotePrefab, content);
                RectTransform rect = longNoteObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(xPos, yPos);
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, endYPos);
                allNotes.Add(longNoteObject);
                Debug.Log("롱노트 배치!");
            }
        }
        Debug.Log("노트 배치 완료!");
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
                    allNotes.Remove(child.gameObject);
                    Destroy(child.gameObject);

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
        return;
    }

        // ➡️ 가장 가까운 스냅 포인트로 이동
        noteRect.anchoredPosition = new Vector2(closestX, closestY);
        allNotes.Add(movingNote);
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
        allNotes.Add(movingNote);
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
            float newYPosition = -pausedTimeMs/2;
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
        float newYPosition = currentTimelinePosition/2;

        // Content 위치 설정 (아래로 이동)
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, -newYPosition);
    }

    public void SaveSheet()
    {
        List<string> sheetData = GenerateNoteSheet();
        // ➡️ 파일 저장 경로 설정 (Application.persistentDataPath 사용)
        string path = Application.persistentDataPath + $"/{title}.txt";

        // ➡️ txt 파일로 저장
        File.WriteAllLines(path, sheetData);

        Debug.Log("노트 시트가 txt 파일로 저장되었습니다: " + path);
    }

    // 노트 시트 생성
    List<string> GenerateNoteSheet()
    {
        List<string> sheetData = new List<string>();

        sheetData.Add("[Description]");
        sheetData.Add($"Title: {title}");
        sheetData.Add("");
        sheetData.Add("[Audio]");
        sheetData.Add($"BPM: {bpm}");
        sheetData.Add($"Offset: {offset}");
        sheetData.Add("");
        sheetData.Add("[Note]");

        List<string> notes = new List<string>();
        List<string> longNotes = new List<string>();

        foreach (GameObject note in allNotes)
        {
            RectTransform noteRect = note.GetComponent<RectTransform>();
            float xPos = noteRect.anchoredPosition.x;
            float yPos = Mathf.Round(noteRect.anchoredPosition.y);
            float height = Mathf.Round(noteRect.rect.height);

            float timeInMs = yPos * 2; // y좌표를 MS 단위로 환산
            float lane = 0;
            if (xPos == -75)
            lane = 1;
            else if (xPos == -25)
            lane = 2;
            else if (xPos == 25)
            lane = 3;
            else if (xPos == 75)
            lane = 4;

            if (height > longNoteHeightThreshold)
            {
                // 롱노트 판별: 노트가 롱노트일 경우 시작과 끝점 기록
                string longNote = $"{timeInMs}, 1, {lane}, {height * 2}";
                longNotes.Add(longNote);
            }
            else
            {
                // 숏노트: 시간, X좌표, 식별 번호 (숏노트는 1로 고정)
                string shortNote = $"{timeInMs}, 0, {lane}";
                notes.Add(shortNote);
            }
        }

        // 숏노트와 롱노트 정보 합치기
        notes.AddRange(longNotes);
        notes.Sort((a, b) => float.Parse(a.Split(',')[0]).CompareTo(float.Parse(b.Split(',')[0]))); // 시간순으로 정렬

        // 시트에 추가
        foreach (string note in notes)
        {
            sheetData.Add(note);
        }

        return sheetData;
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
