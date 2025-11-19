using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Text;
using System;

public class LobbyUI : MonoBehaviour
{
    [SerializeField] Button SingleMatchButton;

    [SerializeField] RectTransform MatchmakingWaitingPanel;
    [SerializeField] TextMeshProUGUI waitingText;

    [SerializeField] CharacterDesc characterDesc;

    [SerializeField] Button QuitButton;
    [SerializeField] TMP_InputField CustomLobbyCode;


    private void Start()
    {
        SingleMatchButton.onClick.AddListener(() =>
        {
            MatchButton();
            if(CustomLobbyCode.text.Length > 0)
            {
                NetworkRunnerHandler.Instance.FindOneVsOneMatch(GetLobbyName());
            }
            else
            {
                NetworkRunnerHandler.Instance.FindOneVsOneMatch();
            }
        });

        QuitButton.onClick.AddListener(() =>
        {
            Application.Quit();
        });
    }

    public void MatchButton()
    {
        SoundManager.Instance.LobbyName = GetLobbyName();
        StartCoroutine(WatingTextRoutine());
        MatchmakingWaitingPanel.gameObject.SetActive(true);
        NetworkRunnerHandler.Instance.SelectedPlayer = 0;
    }

    public string GetLobbyName()
    {
        if(CustomLobbyCode.text.Length > 6)
        {
            return CustomLobbyCode.text.Substring(0, 6);
        }
        else
        {
            return CustomLobbyCode.text;
        }
    }

    private float duration = 0.2f;
    private int len = 1;
    private IEnumerator WatingTextRoutine()
    {
        StringBuilder builder = new();
        string tot = "技记 立加 吝....";
        int baseLen = "技记 立加 吝".Length;
        while(true)
        {
            waitingText.text = tot.Substring(0, baseLen + len);
            yield return new WaitForSeconds(duration);
            len = (len + 1) % 4;
        }
    }
}
