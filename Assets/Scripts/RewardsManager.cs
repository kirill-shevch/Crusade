using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Linq;

public class RewardsManager : MonoBehaviour
{
    public TextMeshProUGUI nodeText;
    public TextMeshProUGUI rewardsText;
    public Button okayButton;
    void Start()
    {
        ShowReward();
        okayButton.onClick.AddListener(OnOkayButtonClicked);
    }
    void ShowReward()
    {
        var game = GameManager.Instance;
        if (!game.ClaimReward()) { rewardsText.text = "Could not save reward. Click to retry."; return; }
        var reward = game.State.reward;
        if (reward == null) return;
        nodeText.text = game.Map.nodes.Single(n => n.id == game.CurrentNodeId).rewardsText;
        rewardsText.text = reward.claimed ? "Reward received: " + reward.value :
            "New unit: " + reward.value + ". Squad is full and merging cannot free a slot. Decline this reward.";
        var label = okayButton.GetComponentInChildren<TMP_Text>();
        if (label != null) label.text = reward.claimed ? "Continue" : "Decline reward";
    }
    void OnOkayButtonClicked()
    {
        var game = GameManager.Instance;
        if (!string.IsNullOrEmpty(game.LastError)) { ShowReward(); return; }
        if (game.DismissReward()) SceneManager.LoadScene(game.ResumeScene);
    }
}
