using UnityEngine;
using TMPro; // 核心：TMP命名空间（已包含）
using System.Collections;
using UnityEngine.UI;

// 奖项数据结构（无修改）
[System.Serializable]
public class Award
{
    public string awardName;        // 奖项名称
    public int awardProbability;    // 中奖概率权重
    public Color awardColor;        // 奖项区域颜色（可选）
}

public class LuckyWheel : MonoBehaviour
{
    [Header("转盘设置")]
    public RectTransform wheelTransform; // 转盘的RectTransform组件
    public float rotateSpeed = 0f;       // 转盘旋转速度
    public float acceleration = 500f;    // 旋转加速度
    public float deceleration = 5f;      // 旋转减速度
    public float minSpeedToStop = 10f;   // 停止旋转的最小速度

    [Header("奖项设置")]
    public Award[] awards;               // 奖项列表
    public TextMeshProUGUI resultText;   // TMP文本（结果显示）
    public Button spinButton;        // 修改：从Button改为TMP_Button

    private bool isSpinning = false;     // 是否正在旋转
    private int totalProbability;        // 总概率权重
    private int selectedAwardIndex = -1; // 选中的奖项索引

    void Start()
    {
        // 计算总概率权重
        totalProbability = 0;
        foreach (Award award in awards)
        {
            totalProbability += award.awardProbability;
        }

        // 绑定按钮点击事件（用法完全兼容）
        spinButton.onClick.AddListener(StartSpin);

        // 初始化结果文本
        resultText.text = "点击开始抽奖！";
    }

    void Update()
    {
        // 转盘旋转逻辑（无修改）
        if (isSpinning)
        {
            wheelTransform.Rotate(0, 0, rotateSpeed * Time.deltaTime);

            if (rotateSpeed > minSpeedToStop)
            {
                rotateSpeed -= deceleration * Time.deltaTime;
            }
            else
            {
                rotateSpeed = 0;
                isSpinning = false;
                spinButton.interactable = true; // TMP_Button同样支持interactable属性
                DetermineAward();
            }
        }
    }

    // 开始抽奖（无修改）
    public void StartSpin()
    {
        if (!isSpinning)
        {
            isSpinning = true;
            spinButton.interactable = false;
            resultText.text = "转盘旋转中...";

            rotateSpeed = Random.Range(1500f, 2500f);
            SelectAwardByProbability();
        }
    }

    // 概率计算（无修改）
    private void SelectAwardByProbability()
    {
        int randomValue = Random.Range(0, totalProbability);
        int currentSum = 0;

        for (int i = 0; i < awards.Length; i++)
        {
            currentSum += awards[i].awardProbability;
            if (randomValue < currentSum)
            {
                selectedAwardIndex = i;
                break;
            }
        }
    }

    // 中奖判定（可保留TMP富文本优化）
    private void DetermineAward()
    {
        if (selectedAwardIndex >= 0 && selectedAwardIndex < awards.Length)
        {
            // 可选：利用TMP富文本美化中奖文字
            string awardName = awards[selectedAwardIndex].awardName;
            Color awardColor = awards[selectedAwardIndex].awardColor;
            string hexColor = "#" + ColorUtility.ToHtmlStringRGB(awardColor);

            resultText.text = $"恭喜你抽中：<color={hexColor}><size=24>{awardName}</size></color>！";

            // 转盘精准停位逻辑
            float anglePerAward = 360f / awards.Length;
            float targetAngle = selectedAwardIndex * anglePerAward;
            StartCoroutine(SnapToTargetAngle(targetAngle));
        }
        else
        {
            resultText.text = "抽奖失败，请重试！";
        }
    }

    // 转盘精准停位（无修改）
    private IEnumerator SnapToTargetAngle(float targetAngle)
    {
        float currentAngle = wheelTransform.rotation.eulerAngles.z % 360;
        float angleDifference = Mathf.DeltaAngle(currentAngle, targetAngle);

        float duration = 0.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0, 1, t);

            wheelTransform.rotation = Quaternion.Euler(
                0, 0,
                Mathf.LerpAngle(currentAngle, targetAngle, t)
            );

            yield return null;
        }

        wheelTransform.rotation = Quaternion.Euler(0, 0, targetAngle);
    }

    // 可选：快速修改TMP按钮文字的方法
    public void SetButtonText(string text)
    {
        // 获取TMP按钮上的TextMeshProUGUI组件并修改文字
        TextMeshProUGUI buttonText = spinButton.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText != null)
        {
            buttonText.text = text;
        }
    }
}