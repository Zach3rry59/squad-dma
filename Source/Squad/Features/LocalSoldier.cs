using Offsets;
using static squad_dma.Game;
using static Vmmsharp.LeechCore;

namespace squad_dma.Source.Squad.Features
{
    public class LocalSoldier : IDisposable
    {
        private readonly ulong _playerController;
        private readonly bool _inGame;
        private readonly RegistredActors _actors;
        private SoldierState _currentState;

        private CancellationTokenSource _cancellationTokenSource;

        private bool _isSuppressionEnabled = Program.Config.DisableSuppression;
        private bool _isInteractionDistancesEnabled = false;
        private bool _isShootingInMainBaseEnabled = false;
        private bool _isSpeedHackEnabled = false;
        private bool _isAirStuckEnabled = false;
        private bool _isHideActorEnabled = false;
        private bool _isQuickZoomEnabled = false;
        private bool _isRapidFireEnabled = false;
        private bool _isInfiniteAmmoEnabled = false;
        private bool _isQuickSwapEnabled = false;
        private bool _isCollisionDisabled = false;

        private bool _isNoRecoilEnabled = Program.Config.NoRecoil;
        private bool _isNoSwayEnabled = Program.Config.NoSway;
        private bool _isNoCameraShakeEnabled = Program.Config.NoCameraShake;

        private ulong _currentWeaponPtr = 0;
        private ulong _currentInventoryPtr = 0;
        private ulong _cachedPlayerState = 0;
        private ulong _cachedSoldierActor = 0;
        private ulong _pawnPtr = 0;
        private ulong _lastNoRecoilWeaponPtr = 0;



        private readonly List<IScatterWriteEntry> _noRecoilAnimEntries = new List<IScatterWriteEntry>
        {
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeapRecoilRelLoc, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeapRecoilRelLoc + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeapRecoilRelLoc + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveRecoilFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.RecoilCanRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.StandRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilMean, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilSigma, 0f),    // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.CrouchRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilMean, 0f), // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma, 0f), // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ProneTransitionRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunch, 0f),          // Pitch
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunch + 4, 0f),      // Yaw
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunch + 8, 0f),      // Roll
        };

        private readonly List<IScatterWriteEntry> _noRecoilWeaponEntries = new List<IScatterWriteEntry>
        {
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.RecoilCameraOffsetFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.RecoilWeaponRelLocFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddMoveRecoil, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxMoveRecoilFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilMean, 0f),   // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilMean + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilMean + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilSigma, 0f),  // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.StandAdsRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilMean, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilSigma, 0f),    // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilMean, 0f),  // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilMean + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilMean + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilSigma, 0f), // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.CrouchAdsRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilMean, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilMean + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilMean + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilSigma, 0f),     // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneRecoilSigma + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilMean, 0f),   // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilMean + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilMean + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilSigma, 0f),  // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilSigma + 4, 0f),// Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneAdsRecoilSigma + 8, 0f),// Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilMean, 0f), // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilMean + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilMean + 8, 0f), // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilSigma, 0f), // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilSigma + 4, 0f), // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ProneTransitionRecoilSigma + 8, 0f), // Z
        };

        private readonly List<IScatterWriteEntry> _noSwayAnimEntries = new List<IScatterWriteEntry>
        {
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveSwayFactorMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SuppressSwayFactorMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator, 0f), // Pitch
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator + 4, 0f), // Yaw
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.WeaponPunchSwayCombinedRotator + 8, 0f), // Roll
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.UnclampedTotalSway, 0f),
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.Sway, 0f), // Pitch
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffset, 0f), // X
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.Sway, 0f), // Pitch
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset, 0f), // X
           // new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
           // new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
        };

        private readonly List<IScatterWriteEntry> _noSwayWeaponEntries = new List<IScatterWriteEntry>
        {
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddMoveSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxMoveSwayFactor, 0f),
           // new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.Sway, 0f), // Pitch
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffset, 0f), // X
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffsetMultiplier, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.UnclampedTotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.TotalSway, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.Sway, 0f), // Pitch
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.Sway + 4, 0f), // Yaw
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.Sway + 8, 0f), // Roll
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset, 0f), // X
           // new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 4, 0f), // Y
            //new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.SwayAlignmentData + Offsets.FSQSwayData.LocationOffset + 8, 0f), // Z
        };

        private readonly List<IScatterWriteEntry> _noSpreadAnimEntries = new List<IScatterWriteEntry>
        {
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation, 0f),      // X
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation + 4, 0f),  // Y
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation + 8, 0f),  // Z
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FinalDeviation + 12, 0f), // W
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.AddMoveDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MoveDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MaxMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.FullStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.LowStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.AddShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.AddShotDeviationFactorAds, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.ShotDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MaxShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinProneAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinProneDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinCrouchAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinCrouchDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinStandAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinStandDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQAnimInstanceSoldier1P.MinProneTransitionDeviation, 0f),
        };

        private readonly List<IScatterWriteEntry> _noSpreadWeaponEntries = new List<IScatterWriteEntry>
        {
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddShotDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddShotDeviationFactorAds, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.ShotDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.LowStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.FullStaminaDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MoveDeviationFactorRelease, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.AddMoveDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MaxMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinMoveDeviationFactor, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinProneAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinProneDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinCrouchAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinCrouchDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinStandAdsDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinStandDeviation, 0f),
            new ScatterWriteDataEntry<float>(0 + Offsets.USQWeaponStaticInfo.MinProneTransitionDeviation, 0f),
        };

        // Movement modes
        private enum EMovementMode : byte
        {
            MOVE_None = 0,
            MOVE_Walking = 1,
            MOVE_NavWalking = 2,
            MOVE_Falling = 3,
            MOVE_Swimming = 4,
            MOVE_Flying = 5,
            MOVE_Custom = 6,
            MOVE_MAX = 7
        }

        // Original Values to restore
        private float _originalUseInteractDistance;
        private float _originalInteractableRadiusMultiplier;
        private float _originalUnderSuppressionPercentage;
        private float _originalMaxSuppressionPercentage;
        private float _originalSuppressionMultiplier;
        private float _originalTimeBetweenShots = 0.0f;
        private float _originalTimeBetweenSingleShots = 0.0f;
        private float _originalFov;
        private float _originalTimeDilation = 0.0f;

        // InstantReload original values
        private byte _originalInfiniteAmmo = 0;
        private byte _originalInfiniteMags = 0;
        private byte _originalCreateProjectileOnServer = 0;

        // AirStuck original values
        private byte _originalMovementMode = 0;
        private byte _originalReplicatedMovementMode = 0;
        private byte _originalReplicateMovement = 0;
        private float _originalMaxFlySpeed = 0.0f;
        private float _originalMaxCustomMovementSpeed = 0.0f;
        private float _originalMaxAcceleration = 0.0f;

        // HideActor original values
        private byte _originalHideActorReplicateMovement = 0;
        private byte _originalHidden = 0;

        // DisableCollision original values
        private byte _originalCollisionEnabled = 0;

        // New values for fast weapon swap
        private float _originalEquipDuration = 0.0f;
        private float _originalUnequipDuration = 0.0f;
        private float _originalCachedEquipDuration = 0.0f;
        private float _originalCachedUnequipDuration = 0.0f;

        // ShootingInMainBase original values
        private bool _originalUsableInMainBase = false;

        private void StartFeatureTimer()
        {
            Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (_isSuppressionEnabled)
                            ApplySuppression();
                        if (_isInteractionDistancesEnabled)
                            ApplyInteractionDistances();
                        if (_isShootingInMainBaseEnabled || _originalUsableInMainBase != false)
                            ApplyShootingInMainBase();
                        if (_isSpeedHackEnabled || _originalTimeDilation != 0.0f)
                            ApplySpeedHack();
                        if (_isAirStuckEnabled || _originalMovementMode != 0)
                            ApplyAirStuck();

                        // Handle DisableCollision, ensuring it's disabled if AirStuck is disabled
                        if (!_isAirStuckEnabled && _isCollisionDisabled)
                        {
                            _isCollisionDisabled = false;
                        }

                        if (_isCollisionDisabled || _originalCollisionEnabled != 0)
                            DisableCollision(_isCollisionDisabled);

                        if (_isHideActorEnabled || _originalHideActorReplicateMovement != 0)
                            HideActor();
                        if (_isRapidFireEnabled || _originalTimeBetweenShots != 0.0f)
                            ApplyRapidFire();
                        if (_isInfiniteAmmoEnabled || _originalInfiniteAmmo != 0)
                            ApplyInfiniteAmmo();
                        if (_isQuickSwapEnabled || _originalEquipDuration != 0.0f)
                            ApplyQuickSwap();
                    }
                    catch { /* Silently fail */ }
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);
        }

        public LocalSoldier(ulong playerController, bool inGame, RegistredActors actors)
        {
            _playerController = playerController;
            _inGame = inGame;
            _actors = actors;
            _cancellationTokenSource = new CancellationTokenSource();
            StartFeatureTimer();
        }

        public void UpdateSoldierState(SoldierState state)
        {
            _currentState = state;
            _currentWeaponPtr = state.WeaponPtr;
            _currentInventoryPtr = state.InventoryPtr;

            if (_isNoRecoilEnabled) ApplyNoRecoilNoSpread();
            if (_isNoSwayEnabled && state.IsAimingDownSights) ApplyNoSway();
            if (_isNoCameraShakeEnabled && state.IsFiring) ApplyNoCameraShake();
        }

        private bool IsLocalPlayerValid()
        {
            try
            {
                if (!_inGame || _playerController == 0 || _currentState.PawnPtr == 0) return false;

                if (_currentState.PawnPtr == 0) return false;
                _pawnPtr = _currentState.PawnPtr;

                _cachedPlayerState = Memory.ReadPtr(_playerController + Controller.PlayerState);
                if (_cachedPlayerState == 0) return false;

                _cachedSoldierActor = Memory.ReadPtr(_cachedPlayerState + ASQPlayerState.Soldier);
                if (_cachedSoldierActor == 0) return false;

                return true;
            }
            catch
            { return false; }
        }

        public void SetNoRecoil(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isNoRecoilEnabled = enable;
            if (enable) ApplyNoRecoilNoSpread();
        }

        public void SetNoSway(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isNoSwayEnabled = enable;
            if (enable && _currentState.IsAimingDownSights) ApplyNoSway();
        }

        public void SetNoCameraShake(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isNoCameraShakeEnabled = enable;
            if (enable && _currentState.IsFiring) ApplyNoCameraShake();
        }

        private bool GetBasePointers(out ulong animInstancePtr, out ulong weaponStaticInfoPtr)
        {
            animInstancePtr = 0;
            weaponStaticInfoPtr = 0;

            if (_pawnPtr == 0 || _currentWeaponPtr == 0) return false;

            _currentInventoryPtr = Memory.ReadPtr(_pawnPtr + Offsets.ASQSoldier.InventoryComponent);
            if (_currentInventoryPtr == 0) return false;

            animInstancePtr = Memory.ReadPtr(_pawnPtr + Offsets.ASQSoldier.CachedAnimInstance1p);
            weaponStaticInfoPtr = Memory.ReadPtr(_currentWeaponPtr + Offsets.ASQWeapon.WeaponStaticInfo);

            return animInstancePtr != 0 && weaponStaticInfoPtr != 0;
        }

        private IScatterWriteEntry UpdateEntryAddress(IScatterWriteEntry entry, ulong baseAddress)
        {
            if (entry is ScatterWriteDataEntry<float> floatEntry)
            {
                return new ScatterWriteDataEntry<float>(baseAddress + (ulong)floatEntry.Address, floatEntry.Data);
            }
            return entry;
        }

        private bool _noPawnLoggedOnce = false;

        public void ApplyNoRecoilNoSpread()
        {
            try
            {
                if (_pawnPtr == 0)
                {
                    _lastNoRecoilWeaponPtr = 0;
                    if (!_noPawnLoggedOnce)
                    {
                        Program.Log("No-recoil/no-spread skipped: No acknowledged pawn.");
                        _noPawnLoggedOnce = true;
                    }
                    return;
                }

                string pawnClassName = Memory.GetActorClassName(_currentState.PawnPtr);
                bool isInVehicle = !pawnClassName.Contains("BP_Soldier");
                if (isInVehicle || _currentWeaponPtr == 0)
                {
                    _lastNoRecoilWeaponPtr = 0;
                    return;
                }

                if (!GetBasePointers(out ulong animInstancePtr, out ulong weaponStaticInfoPtr))
                {
                    Program.Log("No-recoil/no-spread skipped: Failed to get base pointers.");
                    return;
                }

                var scatterEntries = new List<IScatterWriteEntry>();

                if (_currentWeaponPtr != _lastNoRecoilWeaponPtr || _lastNoRecoilWeaponPtr == 0)
                {
                    scatterEntries.AddRange(_noRecoilAnimEntries.Select(e => UpdateEntryAddress(e, animInstancePtr)));
                    scatterEntries.AddRange(_noRecoilWeaponEntries.Select(e => UpdateEntryAddress(e, weaponStaticInfoPtr)));
                    scatterEntries.AddRange(_noSpreadAnimEntries.Select(e => UpdateEntryAddress(e, animInstancePtr)));
                    scatterEntries.AddRange(_noSpreadWeaponEntries.Select(e => UpdateEntryAddress(e, weaponStaticInfoPtr)));
                    _lastNoRecoilWeaponPtr = _currentWeaponPtr;
                    Program.Log($"No-recoil & no-spread applied for weapon 0x{_currentWeaponPtr:X}");

                    // reset the flag since it successfully applied
                    _noPawnLoggedOnce = false;
                }

                if (scatterEntries.Count > 0)
                {
                    Memory.WriteScatter(scatterEntries);
                }
            }
            catch { }
        }

        public void ApplyNoSway()
        {
            try
            {
                if (!GetBasePointers(out ulong animInstancePtr, out ulong weaponStaticInfoPtr)) return;

                var scatterEntries = new List<IScatterWriteEntry>();
                scatterEntries.AddRange(_noSwayAnimEntries.Select(e => UpdateEntryAddress(e, animInstancePtr)));
                scatterEntries.AddRange(_noSwayWeaponEntries.Select(e => UpdateEntryAddress(e, weaponStaticInfoPtr)));

                if (scatterEntries.Count > 0)
                {
                    Memory.WriteScatter(scatterEntries);
                    //Program.Log($"No-sway applied for weapon 0x{_currentWeaponPtr:X}");
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Failed to apply no-sway: {ex.Message}");
            }
        }

        public void ApplyNoCameraShake()
        {
            try
            {
                var scatterEntries = new List<IScatterWriteEntry>();

                ulong cameraManagerPtr = Memory.ReadPtr(_playerController + Offsets.PlayerController.PlayerCameraManager);
                if (cameraManagerPtr == 0) return;
                ulong cameraShakeModPtr = Memory.ReadPtr(cameraManagerPtr + Offsets.Camera.CachedCameraShakeMod);
                if (cameraShakeModPtr != 0)
                {
                    ulong activeShakesDataPtr = Memory.ReadPtr(cameraShakeModPtr + Offsets.UCameraModifier_CameraShake.ActiveShakes);
                    if (activeShakesDataPtr != 0)
                    {
                        int activeShakesCount = Memory.ReadValue<int>(cameraShakeModPtr + Offsets.UCameraModifier_CameraShake.ActiveShakes + 0x8);
                        if (activeShakesCount > 0)
                        {
                            const int shakeInfoSize = 0x18;
                            for (int i = 0; i < activeShakesCount; i++)
                            {
                                ulong shakeBasePtr = Memory.ReadPtr(activeShakesDataPtr + (uint)(i * shakeInfoSize));
                                if (shakeBasePtr != 0)
                                {
                                    scatterEntries.Add(new ScatterWriteDataEntry<float>(shakeBasePtr + Offsets.UCameraShakeBase.ShakeScale, 0f));
                                }
                            }
                        }
                    }
                }

                if (scatterEntries.Count > 0)
                {
                    Memory.WriteScatter(scatterEntries);
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Failed to apply no-shake: {ex.Message}");
            }
        }

        public void SetSuppression(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isSuppressionEnabled = enable;
            ApplySuppression();
        }

        private void ApplySuppression()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (_cachedSoldierActor == 0) return;

                if (_isSuppressionEnabled)
                {
                    if (_originalUnderSuppressionPercentage == 0.0f)
                    {
                        _originalUnderSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage);
                        _originalMaxSuppressionPercentage = Memory.ReadValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage);
                        _originalSuppressionMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier);
                    }

                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, 0.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, 0.0f);
                }
                else if (_originalUnderSuppressionPercentage != 0.0f || _originalMaxSuppressionPercentage != 0.0f || _originalSuppressionMultiplier != 0.0f)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UnderSuppressionPercentage, _originalUnderSuppressionPercentage);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.MaxSuppressionPercentage, _originalMaxSuppressionPercentage);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.SuppressionMultiplier, _originalSuppressionMultiplier);

                    _originalUnderSuppressionPercentage = 0.0f;
                    _originalMaxSuppressionPercentage = 0.0f;
                    _originalSuppressionMultiplier = 0.0f;
                }
                //Program.Log(_isSuppressionEnabled ? "Suppression disabled" : "Suppression restored");
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting suppression: {ex.Message}");
            }
        }

        public void SetInteractionDistances(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isInteractionDistancesEnabled = enable;
            ApplyInteractionDistances();
        }

        private void ApplyInteractionDistances()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                if (_isInteractionDistancesEnabled)
                {
                    if (_originalUseInteractDistance == 0.0f)
                    {
                        _originalUseInteractDistance = Memory.ReadValue<float>(soldierActor + ASQSoldier.UseInteractDistance);
                        _originalInteractableRadiusMultiplier = Memory.ReadValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier);
                    }

                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, 5000.0f);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, 70.0f);
                }
                else if (_originalUseInteractDistance != 0.0f || _originalInteractableRadiusMultiplier != 0.0f)
                {
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.UseInteractDistance, _originalUseInteractDistance);
                    Memory.WriteValue<float>(soldierActor + ASQSoldier.InteractableRadiusMultiplier, _originalInteractableRadiusMultiplier);

                    _originalUseInteractDistance = 0.0f;
                    _originalInteractableRadiusMultiplier = 0.0f;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting interaction distances: {ex.Message}");
            }
        }

        public void SetShootingInMainBase(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isShootingInMainBaseEnabled = enable;
            ApplyShootingInMainBase();
        }

        private void ApplyShootingInMainBase()
        {
            try
            {
                if (!_isShootingInMainBaseEnabled && !_originalUsableInMainBase) return;

                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;

                ulong currentItemStaticInfo = Memory.ReadPtr(inventoryComponent + ASQSoldier.CurrentItemStaticInfo);
                if (currentItemStaticInfo == 0) return;

                if (_isShootingInMainBaseEnabled)
                {
                    if (!_originalUsableInMainBase)
                    {
                        _originalUsableInMainBase = Memory.ReadValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase);
                    }
                    Memory.WriteValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase, true);
                }
                else if (_originalUsableInMainBase != false)
                {
                    Memory.WriteValue<bool>(currentItemStaticInfo + ASQSoldier.bUsableInMainBase, _originalUsableInMainBase);
                    _originalUsableInMainBase = false;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting shooting in main base: {ex.Message}");
            }
        }

        public void SetSpeedHack(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isSpeedHackEnabled = enable;
            ApplySpeedHack();
        }

        private void ApplySpeedHack()
        {
            try
            {
                if (!_isSpeedHackEnabled && _originalTimeDilation == 0.0f) return;

                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                if (_isSpeedHackEnabled)
                {
                    if (_originalTimeDilation == 0.0f)
                    {
                        _originalTimeDilation = Memory.ReadValue<float>(soldierActor + Actor.CustomTimeDilation);
                    }
                    Memory.WriteValue<float>(soldierActor + Actor.CustomTimeDilation, 4.0f);
                }
                else if (_originalTimeDilation != 0.0f)
                {
                    Memory.WriteValue<float>(soldierActor + Actor.CustomTimeDilation, _originalTimeDilation);
                    _originalTimeDilation = 0.0f;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting time dilation: {ex.Message}");
            }
        }

        public void SetAirStuck(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isAirStuckEnabled = enable;
            ApplyAirStuck();
        }

        private void ApplyAirStuck()
        {
            try
            {
                if (!_isAirStuckEnabled && _originalMovementMode == 0) return;

                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ulong characterMovement = Memory.ReadPtr(soldierActor + Character.CharacterMovement);
                if (characterMovement == 0) return;

                if (_isAirStuckEnabled)
                {
                    if (_originalMovementMode == 0)
                    {
                        _originalMovementMode = Memory.ReadValue<byte>(characterMovement + CharacterMovementComponent.MovementMode);
                        _originalReplicatedMovementMode = Memory.ReadValue<byte>(characterMovement + Character.ReplicatedMovementMode);
                        _originalReplicateMovement = Memory.ReadValue<byte>(soldierActor + Actor.bReplicateMovement);
                        _originalMaxFlySpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed);
                        _originalMaxCustomMovementSpeed = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed);
                        _originalMaxAcceleration = Memory.ReadValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration);
                    }

                    Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, (byte)EMovementMode.MOVE_Flying);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, 4000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, 4000.0f);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, 4000.0f);
                }
                else if (_originalMovementMode != 0)
                {
                    Memory.WriteValue<byte>(characterMovement + CharacterMovementComponent.MovementMode, _originalMovementMode);
                    Memory.WriteValue<byte>(characterMovement + Character.ReplicatedMovementMode, _originalReplicatedMovementMode);
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, _originalReplicateMovement);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxFlySpeed, _originalMaxFlySpeed);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxCustomMovementSpeed, _originalMaxCustomMovementSpeed);
                    Memory.WriteValue<float>(characterMovement + CharacterMovementComponent.MaxAcceleration, _originalMaxAcceleration);

                    _originalMovementMode = 0;
                    _originalReplicatedMovementMode = 0;
                    _originalReplicateMovement = 0;
                    _originalMaxFlySpeed = 0.0f;
                    _originalMaxCustomMovementSpeed = 0.0f;
                    _originalMaxAcceleration = 0.0f;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting air stuck: {ex.Message}");
            }
        }

        public void SetQuickZoom(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickZoomEnabled = enable;
            ApplyQuickZoom();
        }

        private void ApplyQuickZoom()
        {
            try
            {
                if (!IsLocalPlayerValid()) return;

                ulong cameraManager = Memory.ReadPtr(_playerController + PlayerController.PlayerCameraManager);
                if (cameraManager == 0) return;

                if (_isQuickZoomEnabled)
                {
                    if (_originalFov == 0.0f)
                    {
                        _originalFov = Memory.ReadValue<float>(cameraManager + PlayerCameraManager.DefaultFOV);
                    }
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, 20.0f);
                }
                else if (_originalFov != 0.0f)
                {
                    Memory.WriteValue<float>(cameraManager + PlayerCameraManager.DefaultFOV, _originalFov);
                    _originalFov = 0.0f;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting Quick Zoom: {ex.Message}");
            }
        }

        public void SetHideActor(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isHideActorEnabled = enable;
            HideActor();
        }

        private void HideActor()
        {
            try
            {
                if (!_isHideActorEnabled && _originalHideActorReplicateMovement == 0) return;

                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                if (_isHideActorEnabled)
                {
                    if (_originalHideActorReplicateMovement == 0)
                    {
                        _originalHideActorReplicateMovement = Memory.ReadValue<byte>(soldierActor + Actor.bReplicateMovement);
                        _originalHidden = Memory.ReadValue<byte>(soldierActor + Actor.bHidden);
                    }

                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, 0);
                    Memory.WriteValue<byte>(soldierActor + Actor.bHidden, 1);
                }
                else if (_originalHideActorReplicateMovement != 0)
                {
                    Memory.WriteValue<byte>(soldierActor + Actor.bReplicateMovement, _originalHideActorReplicateMovement);
                    Memory.WriteValue<byte>(soldierActor + Actor.bHidden, _originalHidden);

                    _originalHideActorReplicateMovement = 0;
                    _originalHidden = 0;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting hide actor: {ex.Message}");
            }
        }

        public void DisableCollision(bool disable)
        {
            if (!IsLocalPlayerValid()) return;

            if (disable && !_isAirStuckEnabled)
            {
                _isCollisionDisabled = false;
                return;
            }

            _isCollisionDisabled = disable;

            try
            {
                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ulong rootComponent = Memory.ReadPtr(soldierActor + Actor.RootComponent);
                if (rootComponent == 0) return;

                ulong bodyInstanceAddr = rootComponent + 0x2c8;

                if (disable)
                {
                    if (_originalCollisionEnabled == 0)
                    {
                        _originalCollisionEnabled = Memory.ReadValue<byte>(bodyInstanceAddr + 0x20);
                    }
                    Memory.WriteValue<byte>(bodyInstanceAddr + 0x20, 0);
                }
                else if (_originalCollisionEnabled != 0)
                {
                    Memory.WriteValue<byte>(bodyInstanceAddr + 0x20, _originalCollisionEnabled);
                    _originalCollisionEnabled = 0;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error {(disable ? "disabling" : "enabling")} collision: {ex.Message}");
            }
        }

        public void SetRapidFire(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isRapidFireEnabled = enable;
            ApplyRapidFire();
        }

        private void ApplyRapidFire()
        {
            try
            {
                if (!_isRapidFireEnabled && _originalTimeBetweenShots == 0.0f) return;

                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;

                ulong currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                if (currentWeapon == 0) return;

                ulong weaponConfigOffset = currentWeapon + ASQWeapon.WeaponConfig;

                if (_isRapidFireEnabled)
                {
                    if (_originalTimeBetweenShots == 0.0f)
                    {
                        _originalTimeBetweenShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots);
                        _originalTimeBetweenSingleShots = Memory.ReadValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots);
                    }

                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, 0.01f);
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, 0.01f);
                }
                else if (_originalTimeBetweenShots != 0.0f)
                {
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenShots, _originalTimeBetweenShots);
                    Memory.WriteValue<float>(weaponConfigOffset + FSQWeaponData.TimeBetweenSingleShots, _originalTimeBetweenSingleShots);

                    _originalTimeBetweenShots = 0.0f;
                    _originalTimeBetweenSingleShots = 0.0f;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting rapid fire: {ex.Message}");
            }
        }

        public void SetInfiniteAmmo(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isInfiniteAmmoEnabled = enable;
            ApplyInfiniteAmmo();
        }

        private void ApplyInfiniteAmmo()
        {
            try
            {
                if (!_isInfiniteAmmoEnabled && _originalInfiniteAmmo == 0) return;

                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ulong inventoryComponent = Memory.ReadPtr(soldierActor + ASQSoldier.InventoryComponent);
                if (inventoryComponent == 0) return;

                ulong currentWeapon = Memory.ReadPtr(inventoryComponent + USQPawnInventoryComponent.CurrentWeapon);
                if (currentWeapon == 0) return;

                ulong weaponConfigOffset = currentWeapon + ASQWeapon.WeaponConfig;

                if (_isInfiniteAmmoEnabled)
                {
                    if (_originalInfiniteAmmo == 0)
                    {
                        _originalInfiniteAmmo = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo);
                        _originalInfiniteMags = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags);
                        _originalCreateProjectileOnServer = Memory.ReadValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer);
                    }

                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, 1);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, 1);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer, 1);
                }
                else if (_originalInfiniteAmmo != 0)
                {
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteAmmo, _originalInfiniteAmmo);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bInfiniteMags, _originalInfiniteMags);
                    Memory.WriteValue<byte>(weaponConfigOffset + FSQWeaponData.bCreateProjectileOnServer, _originalCreateProjectileOnServer);

                    _originalInfiniteAmmo = 0;
                    _originalInfiniteMags = 0;
                    _originalCreateProjectileOnServer = 0;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting infinite ammo: {ex.Message}");
            }
        }

        public void SetQuickSwap(bool enable)
        {
            if (!IsLocalPlayerValid()) return;
            _isQuickSwapEnabled = enable;
            ApplyQuickSwap();
        }

        private void ApplyQuickSwap()
        {
            try
            {
                if (!_isQuickSwapEnabled && _originalEquipDuration == 0.0f) return;

                if (!IsLocalPlayerValid()) return;

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ulong inventoryComponent = _currentInventoryPtr;
                if (inventoryComponent == 0) return;

                ulong currentWeapon = _currentWeaponPtr;
                if (currentWeapon == 0) return;

                if (_isQuickSwapEnabled)
                {
                    if (_originalEquipDuration == 0.0f)
                    {
                        _originalEquipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.EquipDuration);
                        _originalUnequipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration);
                        _originalCachedEquipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration);
                        _originalCachedUnequipDuration = Memory.ReadValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration);
                    }

                    const float FAST_SWAP_VALUE = 0.01f;
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.EquipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration, FAST_SWAP_VALUE);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration, FAST_SWAP_VALUE);
                }
                else if (_originalEquipDuration != 0.0f)
                {
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.EquipDuration, _originalEquipDuration);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.UnequipDuration, _originalUnequipDuration);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedEquipDuration, _originalCachedEquipDuration);
                    Memory.WriteValue<float>(currentWeapon + ASQEquipableItem.CachedUnequipDuration, _originalCachedUnequipDuration);

                    _originalEquipDuration = 0.0f;
                    _originalUnequipDuration = 0.0f;
                    _originalCachedEquipDuration = 0.0f;
                    _originalCachedUnequipDuration = 0.0f;
                }
            }
            catch (Exception ex)
            {
                Program.Log($"Error setting quick swap: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _pawnPtr = 0;
            _currentWeaponPtr = 0;
            _currentInventoryPtr = 0;
            _cachedPlayerState = 0;
            _cachedSoldierActor = 0;
            _pawnPtr = 0;
            _lastNoRecoilWeaponPtr = 0;
            _currentState = new SoldierState();
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }

        private void ReadWeaponInfo(ulong soldierActor, string label)
        {
            try
            {
                ulong inventoryComponent = _currentInventoryPtr;
                if (inventoryComponent == 0) return;

                ulong currentWeapon = _currentWeaponPtr;
                if (currentWeapon == 0) return;

                Program.Log($"{label} Weapon:");

                try
                {
                    int nameIndex = Memory.ReadValue<int>(currentWeapon + 0x18);
                    Dictionary<uint, string> names = Memory.GetNamesById(new List<uint> { (uint)nameIndex });

                    if (names.ContainsKey((uint)nameIndex))
                    {
                        string weaponName = names[(uint)nameIndex];
                        Program.Log($"  - Object: {weaponName}");
                    }
                }
                catch { }

                try
                {
                    ulong itemStaticInfo = Memory.ReadPtr(currentWeapon + ASQEquipableItem.ItemStaticInfo);
                    if (itemStaticInfo != 0)
                    {
                        int staticInfoNameIndex = Memory.ReadValue<int>(itemStaticInfo + 0x18);
                        Dictionary<uint, string> names = Memory.GetNamesById(new List<uint> { (uint)staticInfoNameIndex });

                        if (names.ContainsKey((uint)staticInfoNameIndex))
                        {
                            string infoName = names[(uint)staticInfoNameIndex];
                            Program.Log($"  - Static: {infoName}");
                        }
                    }
                }
                catch { }
            }
            catch { }
        }

        public void ReadCurrentWeapons(bool includeOtherPlayers = false)
        {
            try
            {
                if (!IsLocalPlayerValid()) return;

                Program.Log("=== READING CURRENT WEAPONS ===");

                ulong soldierActor = _cachedSoldierActor;
                if (soldierActor == 0) return;

                ReadWeaponInfo(soldierActor, "Local Player");

                if (includeOtherPlayers)
                {
                    int localTeamId = Memory.ReadValue<int>(_cachedPlayerState + ASQPlayerState.TeamID);
                    Program.Log($"Local player is on team: {localTeamId}");
                }

                Program.Log("=============================");
            }
            catch { }
        }
    }
}