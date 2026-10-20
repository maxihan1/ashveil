// 클라이언트와 서버가 공유하는 도메인 열거형 모음
namespace Ashveil.Shared
{
    public enum ClassType { Warrior, Archer, Mage }

    public enum Gender { Male, Female }

    public enum ItemType { Consumable, Scroll, Weapon, Helmet, Armor, Gloves, Boots }

    public enum EquipSlot { Weapon, Helmet, Armor, Gloves, Boots }

    /// <summary>강화 주문서가 강화할 수 있는 장비 계열.</summary>
    public enum ScrollKind { None, Weapon, Armor }

    public enum ZoneId { Town, Beginner, Intermediate, Boss }

    public enum MonsterType { Imp, VandalImp, ImpBoss }

    public enum Temperament { Peaceful, Retaliate, Aggressive }

    public enum AlignmentState { Innocent, Purple, Chaotic }

    public enum EnhanceResult { Success, Fail, Destroy }

    /// <summary>전투·PK 기록에서 행위자 종류.</summary>
    public enum EntityKind { Character, Bot, Monster, Guard }

    /// <summary>아이템 인스턴스의 소유자 종류.</summary>
    public enum OwnerKind { Character, Bot, Ground }
}
