using UnityEngine;

/// <summary>
/// 卡牌数据模型（定义卡牌核心属性）
/// </summary>
[System.Serializable]
public class Card
{
    // 卡牌基础属性
    public string cardName;    // 卡牌名称
    public int attackValue;    // 攻击力（攻击牌生效）
    public int defenseValue;   // 防御力（防御牌生效，抵消对应攻击力）
    public int healValue;      // 回血量（回血牌生效）
    public CardType cardType;  // 卡牌类型

    // 卡牌类型枚举
    public enum CardType
    {
        Attack,  // 攻击牌
        Defense, // 防御牌
        Heal     // 回血牌
    }

    // 构造函数：快速创建卡牌
    public Card(string name, CardType type, int attack = 0, int defense = 0, int heal = 0)
    {
        cardName = name;
        cardType = type;
        attackValue = attack;
        defenseValue = defense;
        healValue = heal;
    }
}