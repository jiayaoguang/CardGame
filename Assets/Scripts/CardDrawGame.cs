using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

// 奖项数据结构
[System.Serializable]
public class DrawAward
{
    public string awardName;          // 奖项名称
    public Sprite awardSprite;        // 奖项对应的卡牌图片（可选）
    public int probabilityWeight;     // 中奖概率权重（数值越大越容易中）
    public Color textColor = Color.black; // 奖项文字颜色
}

public class CardDrawGame : MonoBehaviour
{

    [Header("核心组件")]
    public RectTransform cardTransform; // 卡牌的RectTransform
    public Button drawButton;       // TMP抽奖按钮
    public TextMeshProUGUI resultText;  // TMP结果文本

    [Header("旋转设置")]
    public float rotateDuration = 2f;   // 旋转时长（固定2秒）
    public float rotateSpeed = 1800f;   // 旋转速度（度/秒）
    public bool use3DRotation = true;   // 是否使用3D翻牌效果

    [Header("奖项配置")]
    public DrawAward[] awards;          // 奖项列表
    public Sprite defaultCardSprite;    // 卡牌背面默认图片

    private bool isDrawing = false;     // 是否正在抽奖
    private int totalProbability;       // 总概率权重
    private DrawAward selectedAward;    // 选中的奖项

    void Start()
    {
        // 初始化概率权重
        totalProbability = 0;
        foreach (var award in awards)
        {
            totalProbability += award.probabilityWeight;
        }

        // 初始化UI
        resultText.text = "点击按钮开始抽奖！";
        if (cardTransform.GetComponent<Image>() != null && defaultCardSprite != null)
        {
            cardTransform.GetComponent<Image>().sprite = defaultCardSprite;
        }

        // 绑定按钮事件
        drawButton.onClick.AddListener(StartDraw);
    }

    // 开始抽奖
    public void StartDraw()
    {
        if (isDrawing || awards.Length == 0) return;

        // 锁定状态
        isDrawing = true;
        drawButton.interactable = false;
        resultText.text = "卡牌旋转中...";

        // 随机选中奖项
        SelectAwardByProbability();

        // 启动旋转协程
        StartCoroutine(RotateCardCoroutine());
    }

    // 按概率选择奖项
    private void SelectAwardByProbability()
    {
        int randomValue = Random.Range(0, totalProbability);
        int currentSum = 0;

        foreach (var award in awards)
        {
            currentSum += award.probabilityWeight;
            if (randomValue < currentSum)
            {
                selectedAward = award;
                break;
            }
        }
    }

    // 卡牌旋转协程（精确控制2秒旋转）
    private IEnumerator RotateCardCoroutine()
    {
        float elapsedTime = 0f;
        Image cardImage = cardTransform.GetComponent<Image>();

        // 旋转过程中使用默认卡牌图片
        if (cardImage != null && defaultCardSprite != null)
        {
            cardImage.sprite = defaultCardSprite;
        }

        // 持续旋转2秒
        while (elapsedTime < rotateDuration)
        {
            elapsedTime += Time.deltaTime;

            // 计算旋转增量
            float rotateAmount = rotateSpeed * Time.deltaTime;

            // 3D旋转效果（绕Y轴）
            if (use3DRotation)
            {
                cardTransform.Rotate(0, rotateAmount, 0, Space.Self);
            }
            // 2D旋转效果（绕Z轴）
            else
            {
                cardTransform.Rotate(0, 0, rotateAmount, Space.Self);
            }

            yield return null;
        }

        // 2秒后停止旋转，显示结果
        StopRotationAndShowResult();
    }

    // 停止旋转并显示中奖结果
    private void StopRotationAndShowResult()
    {
        // 重置卡牌旋转角度（可选，让卡牌摆正）
        cardTransform.rotation = Quaternion.identity;

        // 更新卡牌显示
        Image cardImage = cardTransform.GetComponent<Image>();
        if (cardImage != null)
        {
            // 如果奖项有专属图片则显示，否则用默认图
            cardImage.sprite = selectedAward.awardSprite ?? defaultCardSprite;
        }
        else {
            Debug.Log("cardImage is null .....");
        }

        // 显示中奖文字
        resultText.faceColor = selectedAward.textColor;
        resultText.text = $"恭喜抽中：{selectedAward.awardName}！";

        // 解锁状态
        isDrawing = false;
        drawButton.interactable = true;
    }

    // 可选：重置抽奖状态
    public void ResetDraw()
    {
        isDrawing = false;
        drawButton.interactable = true;
        resultText.text = "点击按钮开始抽奖！";
        resultText.color = Color.black;

        // 重置卡牌
        cardTransform.rotation = Quaternion.identity;
        Image cardImage = cardTransform.GetComponent<Image>();
        if (cardImage != null && defaultCardSprite != null)
        {
            cardImage.sprite = defaultCardSprite;
        }
    }
}