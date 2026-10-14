namespace HireSkilledHelpers.Workers;

/// <summary>Worker perk requirements and effects; saved choices are always gated by current skill XP.</summary>
internal static class WorkerPerkPolicy
{
    public const int UnlockLevel = 5;

    public static bool HasStaminaPerk(WorkerSkillExperience? skills)
        => skills is not null && (Unlocked(skills.Farming) || Unlocked(skills.Mining)
            || Unlocked(skills.Fishing) || Unlocked(skills.Foraging));

    public static bool HasHealthPerk(WorkerSkillExperience? skills)
        => skills is not null && Unlocked(skills.Combat);

    public static float MaximumStamina(WorkerSkillExperience? skills)
        => WorkerStaminaPolicy.Maximum * (HasStaminaPerk(skills) ? 2 : 1);

    public static int MaximumHealth(WorkerSkillExperience? skills, WorkerPerkChoices? choices = null)
        => WorkerCombatPolicy.MaxHealth * (HasHealthPerk(skills) ? 2 : 1)
            * (Active(skills, choices, WorkerSkill.Combat) == 2 ? 4 : 1);

    public static int Experience(WorkerSkillExperience? skills, WorkerSkill skill) => skill switch
    {
        WorkerSkill.Farming => skills?.Farming ?? 0, WorkerSkill.Mining => skills?.Mining ?? 0,
        WorkerSkill.Fishing => skills?.Fishing ?? 0, WorkerSkill.Foraging => skills?.Foraging ?? 0,
        WorkerSkill.Combat => skills?.Combat ?? 0, _ => 0,
    };

    public static bool HasMovementPerk(WorkerSkillExperience? skills)
        => Enum.GetValues<WorkerSkill>().Any(skill => WorkerExperiencePolicy.GetLevel(Experience(skills, skill)) >= 7);

    public static int Active(WorkerSkillExperience? skills, WorkerPerkChoices? choices, WorkerSkill skill)
        => WorkerExperiencePolicy.GetLevel(Experience(skills, skill)) >= 10 ? choices?.Get(skill) ?? 0 : 0;

    public static int Active(WorkerRosterEntry entry, WorkerSkill skill) => Active(entry.Experience, entry.Perks, skill);
    public static int FishingMinutes(WorkerRosterEntry entry) => Active(entry, WorkerSkill.Fishing) switch { 1 => 15, 2 => 60, _ => 30 };
    public static int HarvestMultiplier(int choice, bool forage) => choice == (forage ? 1 : 2) ? (forage ? 5 : 3) : 1;
    public static bool IridiumHarvest(int choice, bool forage) => choice == (forage ? 2 : 1);

    public static bool SynchronizeHealth(WorkerRosterEntry entry)
    {
        int maximum = MaximumHealth(entry.Experience, entry.Perks);
        if (entry.CombatHealth < 0 || entry.CombatMaxHealth <= 0 || entry.CombatMaxHealth == maximum) return false;
        double remainder = double.IsFinite(entry.CombatHealthRemainder) ? Math.Clamp(entry.CombatHealthRemainder, 0, .999999999) : 0;
        double exact = Math.Clamp(entry.CombatHealth - remainder, 0, entry.CombatMaxHealth) / entry.CombatMaxHealth * maximum;
        entry.CombatHealth = (int)Math.Ceiling(exact);
        entry.CombatHealthRemainder = entry.CombatHealth - exact;
        entry.CombatMaxHealth = maximum;
        return true;
    }

    public static bool TrySelect(WorkerRosterEntry entry, WorkerSkill skill, int choice, bool host, bool worldReady)
    {
        if (!host || !worldReady || !Enum.IsDefined(typeof(WorkerSkill), skill) || choice is < 1 or > 2
            || WorkerExperiencePolicy.GetLevel(Experience(entry.Experience, skill)) < 10) return false;
        entry.Perks ??= new();
        entry.Perks.Set(skill, choice);
        return true;
    }

    public static string Name(WorkerSkill skill, int choice) => (skill, choice) switch
    {
        (WorkerSkill.Farming, 1) => "Iridium crops", (WorkerSkill.Farming, 2) => "Triple harvest",
        (WorkerSkill.Mining, 1) => "Double production", (WorkerSkill.Mining, 2) => "Geode specialist",
        (WorkerSkill.Fishing, 1) => "Rapid fishing", (WorkerSkill.Fishing, 2) => "Prize catch",
        (WorkerSkill.Foraging, 1) => "Abundant forage", (WorkerSkill.Foraging, 2) => "Iridium forage",
        (WorkerSkill.Combat, 1) => "Lethal strike", (WorkerSkill.Combat, 2) => "Juggernaut", _ => "Not selected",
    };

    public static string Description(WorkerSkill skill, int choice) => (skill, choice) switch
    {
        (WorkerSkill.Farming, 1) => "Every harvested crop has iridium quality.",
        (WorkerSkill.Farming, 2) => "Each crop harvest produces three times as many items.",
        (WorkerSkill.Mining, 1) => "Independent mining produces twice as many items each hour.",
        (WorkerSkill.Mining, 2) => "Independent mining produces only geodes at twice the normal total output rate. All four types are available at any depth.",
        (WorkerSkill.Fishing, 1) => "Attempt a catch every 15 game minutes: twice the normal rate.",
        (WorkerSkill.Fishing, 2) => "Catch the highest base-price eligible fish every 60 game minutes. Season, time, weather and access rules still apply.",
        (WorkerSkill.Foraging, 1) => "Each gathered forage item yields five times as many items.",
        (WorkerSkill.Foraging, 2) => "Every gathered forage item has iridium quality.",
        (WorkerSkill.Combat, 1) => "One landed strike kills a vulnerable enemy.",
        (WorkerSkill.Combat, 2) => "Four times maximum health. Stacks with Vitality for 800 HP.", _ => "Reach level 10 to choose a perk.",
    };

    private static bool Unlocked(int experience)
        => WorkerExperiencePolicy.GetLevel(experience) >= UnlockLevel;
}
