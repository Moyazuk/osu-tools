namespace PerformanceCalculatorGUI.Components
{
    public enum ProfileSortCriteria
    {
        Aim,
        AimControl,
        Precision,
        Speed,
        Stamina,
        Accuracy,
        Cognition,
        Live,
        Difference,
        Percentage,
        Local,
    }

    public enum CollectionSortCriteria
    {
        Live,
        Index,
        Difference,
        Percentage,
        Local,

        // NEW: same set for collections if you want them
        Aim,
        AimControl,
        Precision,
        Speed,
        Stamina,
        Accuracy,
        Cognition,
    }
}
