using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.TIVirtualFleetState;
using TISpeech.ReviewMode.Readers;

namespace TISpeech.ReviewMode
{
    /// <summary>
    /// The current step in the transfer planning process.
    /// </summary>
    public enum TransferStep
    {
        /// <summary>Select acceleration (theoretical mode only)</summary>
        SelectAcceleration,
        /// <summary>Select delta-V budget (theoretical mode only)</summary>
        SelectDeltaV,
        /// <summary>Select origin body (theoretical mode only)</summary>
        SelectOriginBody,
        /// <summary>Select origin orbit (theoretical mode only)</summary>
        SelectOriginOrbit,
        /// <summary>Select destination type (orbit, hab, fleet intercept)</summary>
        SelectDestinationType,
        /// <summary>Select space body to navigate to</summary>
        SelectBody,
        /// <summary>Select specific orbit around the body</summary>
        SelectOrbit,
        /// <summary>Select a hab/station as destination</summary>
        SelectHab,
        /// <summary>Select an enemy fleet to intercept</summary>
        SelectEnemyFleet,
        /// <summary>View and select from trajectory options</summary>
        ViewTrajectories,
        /// <summary>Confirm the transfer assignment</summary>
        Confirm
    }

    /// <summary>
    /// Destination type options for transfer planning.
    /// </summary>
    public enum TransferDestinationType
    {
        OrbitAroundBody,
        SpecificHab,
        InterceptFleet
    }

    /// <summary>
    /// Sub-mode for multi-step transfer planning.
    /// Supports both fleet-based transfers (actual assignment) and theoretical planning.
    /// </summary>
    public class TransferSubMode
    {
        #region Properties

        /// <summary>
        /// The fleet being assigned a transfer (null for theoretical mode).
        /// </summary>
        public TISpaceFleetState Fleet { get; }

        /// <summary>
        /// Whether this is theoretical planning (no fleet assignment).
        /// </summary>
        public bool IsTheoreticalMode => Fleet == null;

        /// <summary>
        /// Current step in the transfer process.
        /// </summary>
        public TransferStep CurrentStep { get; private set; }

        /// <summary>
        /// Selected destination type.
        /// </summary>
        public TransferDestinationType? DestinationType { get; private set; }

        /// <summary>
        /// Selected origin body (theoretical mode).
        /// </summary>
        public TINaturalSpaceObjectState OriginBody { get; private set; }

        /// <summary>
        /// Selected origin orbit (theoretical mode).
        /// </summary>
        public TIOrbitState OriginOrbit { get; private set; }

        /// <summary>
        /// Selected destination body.
        /// </summary>
        public TINaturalSpaceObjectState SelectedBody { get; private set; }

        /// <summary>
        /// Selected destination orbit.
        /// </summary>
        public TIOrbitState SelectedOrbit { get; private set; }

        /// <summary>
        /// Selected destination hab.
        /// </summary>
        public TIHabState SelectedHab { get; private set; }

        /// <summary>
        /// Selected enemy fleet to intercept.
        /// </summary>
        public TISpaceFleetState TargetFleet { get; private set; }

        /// <summary>
        /// Computed trajectory options.
        /// </summary>
        public Trajectory[] Trajectories { get; private set; }

        /// <summary>
        /// Currently selected trajectory index.
        /// </summary>
        public int SelectedTrajectoryIndex { get; private set; }

        /// <summary>
        /// Current sort mode for trajectories.
        /// </summary>
        public TrajectoryReader.TrajectorySortMode SortMode { get; private set; }

        /// <summary>
        /// Error message if trajectory computation failed.
        /// </summary>
        public string TrajectoryError { get; private set; }

        /// <summary>
        /// Current index in the current list (body, orbit, hab, fleet, or trajectory).
        /// </summary>
        public int CurrentIndex { get; private set; }

        /// <summary>
        /// Callback for speaking announcements.
        /// </summary>
        public Action<string, bool> OnSpeak { get; set; }

        /// <summary>
        /// Callback when transfer is confirmed (passes the trajectory).
        /// </summary>
        public Action<Trajectory> OnTransferConfirmed { get; set; }

        /// <summary>
        /// Callback when transfer mode is cancelled/exited.
        /// </summary>
        public Action OnCancelled { get; set; }

        // Theoretical mode specs
        private float theoreticalAcceleration_mps2;
        private float theoreticalDeltaV_mps;

        #endregion

        #region Current List Data

        private List<SelectionOption> destinationTypeOptions;
        private List<TINaturalSpaceObjectState> bodies;

        // Numeric input buffers for theoretical mode
        private string accelerationInput = "";
        private string deltaVInput = "";
        private List<TIOrbitState> orbits;
        private List<TINaturalSpaceObjectState> originBodies;
        private List<TIOrbitState> originOrbits;
        private List<TIHabState> habs;
        private List<TISpaceFleetState> enemyFleets;

        private readonly OrbitReader orbitReader = new OrbitReader();
        private readonly TrajectoryReader trajectoryReader = new TrajectoryReader();

        #endregion

        #region Constructor

        /// <summary>
        /// Create a transfer sub-mode for a specific fleet.
        /// </summary>
        public TransferSubMode(TISpaceFleetState fleet)
        {
            Fleet = fleet;
            CurrentStep = TransferStep.SelectDestinationType;
            SortMode = TrajectoryReader.TrajectorySortMode.DeltaV;
            InitializeDestinationTypeOptions();
        }

        /// <summary>
        /// Create a transfer sub-mode for theoretical planning.
        /// Starts with acceleration/delta-V input, then origin, then destination.
        /// </summary>
        public TransferSubMode()
        {
            Fleet = null;
            // Theoretical mode starts with acceleration input
            CurrentStep = TransferStep.SelectAcceleration;
            SortMode = TrajectoryReader.TrajectorySortMode.DeltaV;
            InitializeDestinationTypeOptions();
        }

        /// <summary>
        /// Create a transfer sub-mode for theoretical planning with pre-selected origin.
        /// Skips origin selection and goes straight to acceleration/delta-V input, then destination.
        /// </summary>
        /// <param name="originOrbit">The orbit to start from</param>
        public TransferSubMode(TIOrbitState originOrbit)
        {
            Fleet = null;
            OriginOrbit = originOrbit;
            OriginBody = originOrbit?.barycenter as TINaturalSpaceObjectState;
            // Start with acceleration input, then skip to destination
            CurrentStep = TransferStep.SelectAcceleration;
            SortMode = TrajectoryReader.TrajectorySortMode.DeltaV;
            InitializeDestinationTypeOptions();
        }

        /// <summary>
        /// Whether the origin is pre-selected (skips origin selection steps).
        /// </summary>
        public bool HasPreselectedOrigin => Fleet == null && OriginOrbit != null;

        #endregion

        #region Initialization

        private void InitializeDestinationTypeOptions()
        {
            destinationTypeOptions = new List<SelectionOption>
            {
                new SelectionOption
                {
                    Label = "Orbit around a body",
                    DetailText = "Travel to a specific orbit around a planet, moon, or asteroid",
                    Data = TransferDestinationType.OrbitAroundBody
                },
                new SelectionOption
                {
                    Label = "Specific station or base",
                    DetailText = "Travel to dock at a friendly hab",
                    Data = TransferDestinationType.SpecificHab
                }
            };

            // Only add fleet intercept for actual fleets
            if (!IsTheoreticalMode)
            {
                destinationTypeOptions.Add(new SelectionOption
                {
                    Label = "Intercept enemy fleet",
                    DetailText = "Pursue and engage an enemy fleet",
                    Data = TransferDestinationType.InterceptFleet
                });
            }
        }

        private void LoadBodies()
        {
            bodies = new List<TINaturalSpaceObjectState>();

            try
            {
                // Get all space bodies with orbits (planets, moons, asteroids)
                var allBodies = GameStateManager.IterateByClass<TISpaceBodyState>()
                    .Where(b => b != null && !b.isSun && b.orbits != null && b.orbits.Count > 0)
                    .ToList();

                // Get all Lagrange points with orbits
                var allLagrangePoints = GameStateManager.IterateByClass<TILagrangePointState>()
                    .Where(lp => lp != null && lp.orbits != null && lp.orbits.Count > 0)
                    .ToList();

                // Organize hierarchically: planets sorted by distance, then Lagrange points, then moons
                // First pass: get planets (bodies orbiting the sun/barycenter)
                var planets = allBodies
                    .Where(b => b.barycenter == null || b.barycenter.isSun ||
                           b.barycenter.objectType == SpaceObjectType.Star)
                    .OrderBy(b => b.semiMajorAxis_AU)
                    .ToList();

                // Build ordered list: planet, then its Lagrange points, then its moons
                foreach (var planet in planets)
                {
                    bodies.Add(planet);

                    // Find Lagrange points for this planet (where secondaryObject == planet)
                    var lagrangePoints = allLagrangePoints
                        .Where(lp => lp.secondaryObject == planet)
                        .OrderBy(lp => lp.lagrangeValue)
                        .ToList();

                    bodies.AddRange(lagrangePoints.Cast<TINaturalSpaceObjectState>());

                    // Find moons of this planet
                    var moons = allBodies
                        .Where(b => b.barycenter == planet)
                        .OrderBy(b => b.displayName)
                        .ToList();

                    bodies.AddRange(moons.Cast<TINaturalSpaceObjectState>());
                }

                // Add any remaining bodies (shouldn't be many, but safety net)
                var addedBodies = new HashSet<TISpaceBodyState>(
                    bodies.OfType<TISpaceBodyState>());
                var remainingBodies = allBodies.Where(b => !addedBodies.Contains(b)).OrderBy(b => b.displayName);
                bodies.AddRange(remainingBodies.Cast<TINaturalSpaceObjectState>());

                // Add any remaining Lagrange points
                var addedLP = new HashSet<TILagrangePointState>(
                    bodies.OfType<TILagrangePointState>());
                var remainingLP = allLagrangePoints.Where(lp => !addedLP.Contains(lp)).OrderBy(lp => lp.displayName);
                bodies.AddRange(remainingLP.Cast<TINaturalSpaceObjectState>());

                int bodyCount = bodies.OfType<TISpaceBodyState>().Count();
                int lpCount = bodies.OfType<TILagrangePointState>().Count();
                MelonLogger.Msg($"TransferSubMode: Loaded {bodyCount} bodies and {lpCount} Lagrange points (hierarchical order)");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error loading bodies: {ex.Message}");
            }
        }

        private void LoadOrbitsForBody(TINaturalSpaceObjectState body)
        {
            orbits = OrbitReader.GetOrbitsAroundBody(body);
            MelonLogger.Msg($"TransferSubMode: Loaded {orbits.Count} orbits around {body.displayName}");
        }

        private void LoadHabs()
        {
            habs = new List<TIHabState>();

            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null) return;

                // Get all habs that allow docking and are friendly
                habs = GameStateManager.IterateByClass<TIHabState>()
                    .Where(h => h != null && !h.IsBase &&
                           (h.faction == faction || h.faction?.permanentAlly(faction) == true))
                    .OrderBy(h => h.ref_spaceBody?.displayName ?? "")
                    .ThenBy(h => h.displayName)
                    .ToList();

                MelonLogger.Msg($"TransferSubMode: Loaded {habs.Count} accessible habs");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error loading habs: {ex.Message}");
            }
        }

        private void LoadEnemyFleets()
        {
            enemyFleets = new List<TISpaceFleetState>();

            try
            {
                var faction = GameControl.control?.activePlayer;
                if (faction == null) return;

                enemyFleets = FleetReader.GetKnownEnemyFleets(faction);
                MelonLogger.Msg($"TransferSubMode: Loaded {enemyFleets.Count} enemy fleets");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error loading enemy fleets: {ex.Message}");
            }
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Get the current list count. Returns 0 for input steps (acceleration/delta-V).
        /// </summary>
        public int CurrentListCount
        {
            get
            {
                return CurrentStep switch
                {
                    TransferStep.SelectAcceleration => 0, // Input mode, not list
                    TransferStep.SelectDeltaV => 0, // Input mode, not list
                    TransferStep.SelectOriginBody => originBodies?.Count ?? 0,
                    TransferStep.SelectOriginOrbit => originOrbits?.Count ?? 0,
                    TransferStep.SelectDestinationType => destinationTypeOptions?.Count ?? 0,
                    TransferStep.SelectBody => bodies?.Count ?? 0,
                    TransferStep.SelectOrbit => orbits?.Count ?? 0,
                    TransferStep.SelectHab => habs?.Count ?? 0,
                    TransferStep.SelectEnemyFleet => enemyFleets?.Count ?? 0,
                    TransferStep.ViewTrajectories => Trajectories?.Length ?? 0,
                    TransferStep.Confirm => 2, // Yes/No
                    _ => 0
                };
            }
        }

        /// <summary>
        /// Whether the current step is a numeric input step.
        /// </summary>
        public bool IsInputStep => CurrentStep == TransferStep.SelectAcceleration ||
                                   CurrentStep == TransferStep.SelectDeltaV;

        /// <summary>
        /// Get the current input buffer for the active input step.
        /// </summary>
        public string CurrentInputBuffer
        {
            get
            {
                return CurrentStep switch
                {
                    TransferStep.SelectAcceleration => accelerationInput,
                    TransferStep.SelectDeltaV => deltaVInput,
                    _ => ""
                };
            }
        }

        /// <summary>
        /// Handle a digit key press during input mode.
        /// </summary>
        public void HandleDigit(char digit)
        {
            if (!IsInputStep) return;
            if (!char.IsDigit(digit)) return;

            // Limit input length
            string current = CurrentInputBuffer;
            if (current.Length >= 8) return;

            if (CurrentStep == TransferStep.SelectAcceleration)
                accelerationInput += digit;
            else if (CurrentStep == TransferStep.SelectDeltaV)
                deltaVInput += digit;

            AnnounceCurrentInput();
        }

        /// <summary>
        /// Handle decimal point key press during input mode.
        /// </summary>
        public void HandleDecimal()
        {
            if (!IsInputStep) return;

            string current = CurrentInputBuffer;

            // Only one decimal point allowed
            if (current.Contains(".")) return;
            if (current.Length >= 8) return;

            // Add leading zero if starting with decimal
            string toAdd = current.Length == 0 ? "0." : ".";

            if (CurrentStep == TransferStep.SelectAcceleration)
                accelerationInput += toAdd;
            else if (CurrentStep == TransferStep.SelectDeltaV)
                deltaVInput += toAdd;

            AnnounceCurrentInput();
        }

        /// <summary>
        /// Handle backspace key press during input mode.
        /// </summary>
        public void HandleBackspace()
        {
            if (!IsInputStep) return;

            if (CurrentStep == TransferStep.SelectAcceleration && accelerationInput.Length > 0)
            {
                accelerationInput = accelerationInput.Substring(0, accelerationInput.Length - 1);
                AnnounceCurrentInput();
            }
            else if (CurrentStep == TransferStep.SelectDeltaV && deltaVInput.Length > 0)
            {
                deltaVInput = deltaVInput.Substring(0, deltaVInput.Length - 1);
                AnnounceCurrentInput();
            }
        }

        /// <summary>
        /// Announce the current input value.
        /// </summary>
        private void AnnounceCurrentInput()
        {
            string value = CurrentInputBuffer;
            if (string.IsNullOrEmpty(value))
                value = "empty";

            string unit = CurrentStep == TransferStep.SelectAcceleration ? "milligees" : "km/s";
            OnSpeak?.Invoke($"{value} {unit}", false);
        }

        /// <summary>
        /// Move to the previous item in the current list.
        /// </summary>
        public void Previous()
        {
            CurrentIndex--;
            if (CurrentIndex < 0)
                CurrentIndex = Math.Max(0, CurrentListCount - 1);

            if (CurrentStep == TransferStep.ViewTrajectories)
                SelectedTrajectoryIndex = CurrentIndex;
        }

        /// <summary>
        /// Move to the next item in the current list.
        /// </summary>
        public void Next()
        {
            CurrentIndex++;
            if (CurrentIndex >= CurrentListCount)
                CurrentIndex = 0;

            if (CurrentStep == TransferStep.ViewTrajectories)
                SelectedTrajectoryIndex = CurrentIndex;
        }

        /// <summary>
        /// Select the current item and drill down / proceed.
        /// </summary>
        public void Select()
        {
            switch (CurrentStep)
            {
                case TransferStep.SelectAcceleration:
                    SelectAcceleration();
                    break;

                case TransferStep.SelectDeltaV:
                    SelectDeltaV();
                    break;

                case TransferStep.SelectOriginBody:
                    SelectOriginBody();
                    break;

                case TransferStep.SelectOriginOrbit:
                    SelectOriginOrbit();
                    break;

                case TransferStep.SelectDestinationType:
                    SelectDestinationType();
                    break;

                case TransferStep.SelectBody:
                    SelectBody();
                    break;

                case TransferStep.SelectOrbit:
                    SelectOrbit();
                    break;

                case TransferStep.SelectHab:
                    SelectHab();
                    break;

                case TransferStep.SelectEnemyFleet:
                    SelectEnemyFleet();
                    break;

                case TransferStep.ViewTrajectories:
                    // Select trajectory and go to confirm
                    if (Trajectories != null && SelectedTrajectoryIndex < Trajectories.Length)
                    {
                        CurrentStep = TransferStep.Confirm;
                        CurrentIndex = 0;
                        AnnounceCurrentState();
                    }
                    break;

                case TransferStep.Confirm:
                    if (CurrentIndex == 0) // Yes
                    {
                        ConfirmTransfer();
                    }
                    else // No
                    {
                        OnCancelled?.Invoke();
                    }
                    break;
            }
        }

        /// <summary>
        /// Go back to the previous step.
        /// </summary>
        public void Back()
        {
            switch (CurrentStep)
            {
                case TransferStep.SelectAcceleration:
                    // Exit transfer mode (first step in theoretical mode)
                    OnCancelled?.Invoke();
                    break;

                case TransferStep.SelectDeltaV:
                    // Go back to acceleration selection
                    CurrentStep = TransferStep.SelectAcceleration;
                    CurrentIndex = 0; // Reset to start
                    AnnounceCurrentState();
                    break;

                case TransferStep.SelectOriginBody:
                    // Go back to delta-V selection
                    CurrentStep = TransferStep.SelectDeltaV;
                    CurrentIndex = 0;
                    AnnounceCurrentState();
                    break;

                case TransferStep.SelectOriginOrbit:
                    CurrentStep = TransferStep.SelectOriginBody;
                    CurrentIndex = originBodies?.IndexOf(OriginBody) ?? 0;
                    if (CurrentIndex < 0) CurrentIndex = 0;
                    AnnounceCurrentState();
                    break;

                case TransferStep.SelectDestinationType:
                    if (IsTheoreticalMode)
                    {
                        if (HasPreselectedOrigin)
                        {
                            // Go back to delta-V selection (origin was pre-selected)
                            CurrentStep = TransferStep.SelectDeltaV;
                            CurrentIndex = 0;
                        }
                        else
                        {
                            // Go back to origin orbit selection
                            CurrentStep = TransferStep.SelectOriginOrbit;
                            CurrentIndex = originOrbits?.IndexOf(OriginOrbit) ?? 0;
                            if (CurrentIndex < 0) CurrentIndex = 0;
                        }
                    }
                    else
                    {
                        // Exit transfer mode (first step in fleet mode)
                        OnCancelled?.Invoke();
                        return;
                    }
                    AnnounceCurrentState();
                    break;

                case TransferStep.SelectBody:
                case TransferStep.SelectHab:
                case TransferStep.SelectEnemyFleet:
                    CurrentStep = TransferStep.SelectDestinationType;
                    CurrentIndex = 0;
                    AnnounceCurrentState();
                    break;

                case TransferStep.SelectOrbit:
                    CurrentStep = TransferStep.SelectBody;
                    CurrentIndex = bodies?.IndexOf(SelectedBody as TISpaceBodyState) ?? 0;
                    if (CurrentIndex < 0) CurrentIndex = 0;
                    AnnounceCurrentState();
                    break;

                case TransferStep.ViewTrajectories:
                    // Go back based on destination type
                    if (DestinationType == TransferDestinationType.OrbitAroundBody)
                    {
                        CurrentStep = TransferStep.SelectOrbit;
                        CurrentIndex = orbits?.IndexOf(SelectedOrbit) ?? 0;
                    }
                    else if (DestinationType == TransferDestinationType.SpecificHab)
                    {
                        CurrentStep = TransferStep.SelectHab;
                        CurrentIndex = habs?.IndexOf(SelectedHab) ?? 0;
                    }
                    else
                    {
                        CurrentStep = TransferStep.SelectEnemyFleet;
                        CurrentIndex = enemyFleets?.IndexOf(TargetFleet) ?? 0;
                    }
                    if (CurrentIndex < 0) CurrentIndex = 0;
                    AnnounceCurrentState();
                    break;

                case TransferStep.Confirm:
                    CurrentStep = TransferStep.ViewTrajectories;
                    CurrentIndex = SelectedTrajectoryIndex;
                    AnnounceCurrentState();
                    break;
            }
        }

        /// <summary>
        /// Cycle trajectory sort mode.
        /// </summary>
        public void CycleSortMode()
        {
            if (CurrentStep != TransferStep.ViewTrajectories || Trajectories == null)
                return;

            SortMode = TrajectoryReader.CycleSortMode(SortMode);
            Trajectories = TrajectoryReader.SortTrajectories(Trajectories, SortMode);
            SelectedTrajectoryIndex = 0;
            CurrentIndex = 0;

            OnSpeak?.Invoke($"Sorted by {TrajectoryReader.GetSortModeDisplayName(SortMode)}", true);
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Jump to item by first letter.
        /// </summary>
        public void JumpToLetter(char letter)
        {
            letter = char.ToUpperInvariant(letter);

            int foundIndex = -1;

            switch (CurrentStep)
            {
                case TransferStep.SelectOriginBody:
                    foundIndex = FindByLetter(originBodies, b => b.displayName, letter);
                    break;

                case TransferStep.SelectOriginOrbit:
                    foundIndex = FindByLetter(originOrbits, o => o.displayName, letter);
                    break;

                case TransferStep.SelectBody:
                    foundIndex = FindByLetter(bodies, b => b.displayName, letter);
                    break;

                case TransferStep.SelectOrbit:
                    foundIndex = FindByLetter(orbits, o => o.displayName, letter);
                    break;

                case TransferStep.SelectHab:
                    foundIndex = FindByLetter(habs, h => h.displayName, letter);
                    break;

                case TransferStep.SelectEnemyFleet:
                    foundIndex = FindByLetter(enemyFleets, f => f.displayName, letter);
                    break;
            }

            if (foundIndex >= 0)
            {
                CurrentIndex = foundIndex;
                AnnounceCurrentItem();
            }
        }

        private int FindByLetter<T>(List<T> list, Func<T, string> getName, char letter)
        {
            if (list == null) return -1;

            // Search from current + 1 to end
            for (int i = CurrentIndex + 1; i < list.Count; i++)
            {
                string name = getName(list[i]);
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return i;
            }

            // Wrap around
            for (int i = 0; i <= CurrentIndex; i++)
            {
                string name = getName(list[i]);
                if (!string.IsNullOrEmpty(name) && char.ToUpperInvariant(name[0]) == letter)
                    return i;
            }

            return -1;
        }

        #endregion

        #region Selection Methods

        private void SelectAcceleration()
        {
            // Parse the acceleration input (in milligees)
            if (string.IsNullOrEmpty(accelerationInput))
            {
                OnSpeak?.Invoke("Please enter an acceleration value", true);
                return;
            }

            if (!float.TryParse(accelerationInput, out float accel_mg) || accel_mg <= 0)
            {
                OnSpeak?.Invoke("Invalid acceleration. Enter a positive number.", true);
                return;
            }

            // Convert milligees to m/s² (1 mg = 0.00980665 m/s²)
            theoreticalAcceleration_mps2 = accel_mg * 0.00980665f;
            MelonLogger.Msg($"Selected acceleration: {accel_mg} mg ({theoreticalAcceleration_mps2} m/s²)");

            CurrentStep = TransferStep.SelectDeltaV;
            CurrentIndex = 0;
            AnnounceCurrentState();
        }

        private void SelectDeltaV()
        {
            // Parse the delta-V input (in km/s)
            if (string.IsNullOrEmpty(deltaVInput))
            {
                OnSpeak?.Invoke("Please enter a delta-V value", true);
                return;
            }

            if (!float.TryParse(deltaVInput, out float dv_kps) || dv_kps <= 0)
            {
                OnSpeak?.Invoke("Invalid delta-V. Enter a positive number.", true);
                return;
            }

            // Convert km/s to m/s
            theoreticalDeltaV_mps = dv_kps * 1000f;
            MelonLogger.Msg($"Selected delta-V: {dv_kps} km/s ({theoreticalDeltaV_mps} m/s)");

            // If origin is pre-selected, skip to destination selection
            if (HasPreselectedOrigin)
            {
                CurrentStep = TransferStep.SelectDestinationType;
                CurrentIndex = 0;
                AnnounceCurrentState();
                return;
            }

            // Otherwise, load bodies for origin selection
            LoadBodies();
            originBodies = bodies;

            CurrentStep = TransferStep.SelectOriginBody;
            CurrentIndex = 0;
            AnnounceCurrentState();
        }

        private void SelectOriginBody()
        {
            if (originBodies == null || CurrentIndex >= originBodies.Count)
                return;

            OriginBody = originBodies[CurrentIndex];

            // Load orbits for origin body
            originOrbits = OrbitReader.GetOrbitsAroundBody(OriginBody);

            if (originOrbits.Count == 0)
            {
                OnSpeak?.Invoke($"No orbits defined around {OriginBody.displayName}", true);
                return;
            }

            CurrentStep = TransferStep.SelectOriginOrbit;
            CurrentIndex = 0;
            AnnounceCurrentState();
        }

        private void SelectOriginOrbit()
        {
            if (originOrbits == null || CurrentIndex >= originOrbits.Count)
                return;

            OriginOrbit = originOrbits[CurrentIndex];

            // Now proceed to destination selection
            CurrentStep = TransferStep.SelectDestinationType;
            CurrentIndex = 0;
            AnnounceCurrentState();
        }

        private void SelectDestinationType()
        {
            if (destinationTypeOptions == null || CurrentIndex >= destinationTypeOptions.Count)
                return;

            DestinationType = (TransferDestinationType)destinationTypeOptions[CurrentIndex].Data;

            switch (DestinationType)
            {
                case TransferDestinationType.OrbitAroundBody:
                    LoadBodies();
                    CurrentStep = TransferStep.SelectBody;
                    CurrentIndex = 0;
                    break;

                case TransferDestinationType.SpecificHab:
                    LoadHabs();
                    CurrentStep = TransferStep.SelectHab;
                    CurrentIndex = 0;
                    break;

                case TransferDestinationType.InterceptFleet:
                    LoadEnemyFleets();
                    CurrentStep = TransferStep.SelectEnemyFleet;
                    CurrentIndex = 0;
                    break;
            }

            AnnounceCurrentState();
        }

        private void SelectBody()
        {
            if (bodies == null || CurrentIndex >= bodies.Count)
                return;

            SelectedBody = bodies[CurrentIndex];
            LoadOrbitsForBody(SelectedBody);

            if (orbits.Count == 0)
            {
                OnSpeak?.Invoke($"No orbits defined around {SelectedBody.displayName}", true);
                return;
            }

            CurrentStep = TransferStep.SelectOrbit;
            CurrentIndex = 0;
            AnnounceCurrentState();
        }

        private void SelectOrbit()
        {
            if (orbits == null || CurrentIndex >= orbits.Count)
                return;

            SelectedOrbit = orbits[CurrentIndex];
            ComputeTrajectories(SelectedOrbit);
        }

        private void SelectHab()
        {
            if (habs == null || CurrentIndex >= habs.Count)
                return;

            SelectedHab = habs[CurrentIndex];
            ComputeTrajectories(SelectedHab);
        }

        private void SelectEnemyFleet()
        {
            if (enemyFleets == null || CurrentIndex >= enemyFleets.Count)
                return;

            TargetFleet = enemyFleets[CurrentIndex];
            ComputeTrajectories(TargetFleet);
        }

        #endregion

        #region Trajectory Computation

        private void ComputeTrajectories(TIGameState target)
        {
            OnSpeak?.Invoke("Computing trajectories...", true);

            try
            {
                Trajectories = null;
                TrajectoryError = null;

                IMobileAsset actor;

                if (IsTheoreticalMode)
                {
                    // Create a virtual fleet for theoretical computation
                    if (OriginOrbit == null)
                    {
                        TrajectoryError = "No origin orbit selected";
                        OnSpeak?.Invoke(TrajectoryError, true);
                        return;
                    }

                    var faction = GameControl.control?.activePlayer;
                    if (faction == null)
                    {
                        TrajectoryError = "No active player";
                        OnSpeak?.Invoke(TrajectoryError, true);
                        return;
                    }

                    // Create virtual fleet with user-specified specs
                    actor = new TIVirtualSpaceFleet(
                        OriginOrbit,
                        theoreticalAcceleration_mps2,
                        theoreticalDeltaV_mps,
                        faction
                    );

                    MelonLogger.Msg($"Created virtual fleet: accel={theoreticalAcceleration_mps2} m/s², DV={theoreticalDeltaV_mps} m/s");
                }
                else
                {
                    // Use actual fleet
                    actor = Fleet;
                }

                // Use MasterTransferPlanner for trajectory computation
                double lowestDV_kps;
                var result = MasterTransferPlanner.RequestTrajectories(
                    actor,
                    target,
                    64,
                    OnTrajectoriesComputed,
                    out lowestDV_kps
                );

                if (result.Result != TransferResult.Outcome.Success)
                {
                    TrajectoryError = GetTransferResultMessage(result);
                    OnSpeak?.Invoke(TrajectoryError, true);
                }
            }
            catch (Exception ex)
            {
                TrajectoryError = $"Error computing trajectories: {ex.Message}";
                MelonLogger.Error($"ComputeTrajectories error: {ex}");
                OnSpeak?.Invoke("Error computing trajectories", true);
            }
        }

        private void OnTrajectoriesComputed(Trajectory[] trajectories)
        {
            if (trajectories == null || trajectories.Length == 0)
            {
                TrajectoryError = "No valid trajectories found";
                OnSpeak?.Invoke(TrajectoryError, true);
                return;
            }

            Trajectories = TrajectoryReader.SortTrajectories(trajectories, SortMode);
            SelectedTrajectoryIndex = 0;
            CurrentStep = TransferStep.ViewTrajectories;
            CurrentIndex = 0;

            OnSpeak?.Invoke($"Found {Trajectories.Length} trajectory options, sorted by {TrajectoryReader.GetSortModeDisplayName(SortMode)}", true);
            AnnounceCurrentItem();
        }

        private string GetTransferResultMessage(TransferResult result)
        {
            if (result == null) return "Unknown error";

            return result.Result switch
            {
                TransferResult.Outcome.Success => "Success",
                TransferResult.Outcome.Fail_InsufficientDV => "Insufficient delta-V for this transfer",
                TransferResult.Outcome.Fail_InsufficientAcceleration => "Insufficient acceleration for this transfer",
                TransferResult.Outcome.Fail_AttemptedFleetInterceptThatWouldCauseTargetingLoop => "Cannot intercept - would cause targeting loop",
                TransferResult.Outcome.Fail_AttemptedFleetInterceptInMicrothrust => "Cannot intercept fleet - acceleration too low",
                TransferResult.Outcome.Fail_ExceedsMaxDuration => "Transfer would take too long",
                TransferResult.Outcome.Fail_WouldCollideWithBody => "Would collide with celestial body",
                TransferResult.Outcome.Fail_WouldExceedHillRadius => "Would exceed gravitational influence",
                _ => result.ToString()
            };
        }

        #endregion

        #region Confirmation

        private void ConfirmTransfer()
        {
            if (IsTheoreticalMode)
            {
                OnSpeak?.Invoke("Theoretical mode - no transfer to assign", true);
                OnCancelled?.Invoke();
                return;
            }

            if (Trajectories == null || SelectedTrajectoryIndex >= Trajectories.Length)
            {
                OnSpeak?.Invoke("No trajectory selected", true);
                return;
            }

            var trajectory = Trajectories[SelectedTrajectoryIndex];
            OnTransferConfirmed?.Invoke(trajectory);
        }

        #endregion

        #region Announcements

        /// <summary>
        /// Announce the current state/step.
        /// </summary>
        public void AnnounceCurrentState()
        {
            string announcement = GetStepAnnouncement();
            OnSpeak?.Invoke(announcement, true);
            AnnounceCurrentItem();
        }

        /// <summary>
        /// Get the announcement for the current step.
        /// </summary>
        public string GetStepAnnouncement()
        {
            return CurrentStep switch
            {
                TransferStep.SelectAcceleration => "Enter acceleration in milligees. Type digits, Enter to confirm.",
                TransferStep.SelectDeltaV => "Enter delta-V in km/s. Type digits, Enter to confirm.",
                TransferStep.SelectOriginBody => $"Select origin body ({originBodies?.Count ?? 0} options)",
                TransferStep.SelectOriginOrbit => $"Select origin orbit around {OriginBody?.displayName} ({originOrbits?.Count ?? 0} options)",
                TransferStep.SelectDestinationType => "Select destination type",
                TransferStep.SelectBody => $"Select destination body ({bodies?.Count ?? 0} options)",
                TransferStep.SelectOrbit => $"Select orbit around {SelectedBody?.displayName} ({orbits?.Count ?? 0} options)",
                TransferStep.SelectHab => $"Select station or base ({habs?.Count ?? 0} options)",
                TransferStep.SelectEnemyFleet => $"Select enemy fleet to intercept ({enemyFleets?.Count ?? 0} options)",
                TransferStep.ViewTrajectories => $"Select trajectory ({Trajectories?.Length ?? 0} options). Tab to change sort.",
                TransferStep.Confirm => GetConfirmationPrompt(),
                _ => "Transfer planning"
            };
        }

        /// <summary>
        /// Announce the current item in the current list.
        /// </summary>
        public void AnnounceCurrentItem()
        {
            string announcement = GetCurrentItemAnnouncement();
            if (!string.IsNullOrEmpty(announcement))
            {
                OnSpeak?.Invoke(announcement, false);
            }
        }

        /// <summary>
        /// Get the announcement for the current item.
        /// </summary>
        public string GetCurrentItemAnnouncement()
        {
            // For input steps, show current input value
            if (IsInputStep)
            {
                return GetInputAnnouncement();
            }

            int count = CurrentListCount;
            if (count == 0) return "No options available";

            string posStr = $"({CurrentIndex + 1} of {count})";

            return CurrentStep switch
            {
                TransferStep.SelectOriginBody => GetOriginBodyAnnouncement(posStr),
                TransferStep.SelectOriginOrbit => GetOriginOrbitAnnouncement(posStr),
                TransferStep.SelectDestinationType => GetDestinationTypeAnnouncement(posStr),
                TransferStep.SelectBody => GetBodyAnnouncement(posStr),
                TransferStep.SelectOrbit => GetOrbitAnnouncement(posStr),
                TransferStep.SelectHab => GetHabAnnouncement(posStr),
                TransferStep.SelectEnemyFleet => GetEnemyFleetAnnouncement(posStr),
                TransferStep.ViewTrajectories => GetTrajectoryAnnouncement(posStr),
                TransferStep.Confirm => GetConfirmOptionAnnouncement(posStr),
                _ => ""
            };
        }

        private string GetInputAnnouncement()
        {
            string value = CurrentInputBuffer;
            if (string.IsNullOrEmpty(value))
                value = "empty";

            if (CurrentStep == TransferStep.SelectAcceleration)
                return $"Current: {value} milligees";
            else
                return $"Current: {value} km/s";
        }

        private string GetOriginBodyAnnouncement(string posStr)
        {
            if (originBodies == null || CurrentIndex >= originBodies.Count)
                return "";

            var body = originBodies[CurrentIndex];
            return $"{FormatBodyName(body)}, {posStr}";
        }

        private string GetOriginOrbitAnnouncement(string posStr)
        {
            if (originOrbits == null || CurrentIndex >= originOrbits.Count)
                return "";

            var orbit = originOrbits[CurrentIndex];
            return $"{orbitReader.ReadSummary(orbit)}, {posStr}";
        }

        private string GetDestinationTypeAnnouncement(string posStr)
        {
            if (destinationTypeOptions == null || CurrentIndex >= destinationTypeOptions.Count)
                return "";

            var option = destinationTypeOptions[CurrentIndex];
            return $"{option.Label}, {posStr}. {option.DetailText}";
        }

        private string GetBodyAnnouncement(string posStr)
        {
            if (bodies == null || CurrentIndex >= bodies.Count)
                return "";

            var body = bodies[CurrentIndex];
            return $"{FormatBodyName(body)}, {posStr}";
        }

        private string FormatBodyName(TINaturalSpaceObjectState body)
        {
            if (body == null) return "Unknown";

            int orbitCount = body.orbits?.Count ?? 0;
            string orbitsStr = $"{orbitCount} orbit{(orbitCount != 1 ? "s" : "")}";

            // Check if it's a Lagrange point
            if (body.isLagrangePointState)
            {
                var lp = body as TILagrangePointState;
                string lpValue = lp?.lagrangeValue.ToString() ?? "L?";
                string relatedBody = lp?.secondaryObject?.displayName ?? "Unknown";
                return $"{body.displayName} ({lpValue} of {relatedBody}), {orbitsStr}";
            }

            return $"{body.displayName}, {orbitsStr}";
        }

        private string GetOrbitAnnouncement(string posStr)
        {
            if (orbits == null || CurrentIndex >= orbits.Count)
                return "";

            var orbit = orbits[CurrentIndex];
            return $"{orbitReader.ReadSummary(orbit)}, {posStr}";
        }

        private string GetHabAnnouncement(string posStr)
        {
            if (habs == null || CurrentIndex >= habs.Count)
                return "";

            var hab = habs[CurrentIndex];
            string location = hab.ref_spaceBody?.displayName ?? hab.ref_naturalSpaceObject?.displayName ?? "";
            return $"{hab.displayName} at {location}, {posStr}";
        }

        private string GetEnemyFleetAnnouncement(string posStr)
        {
            if (enemyFleets == null || CurrentIndex >= enemyFleets.Count)
                return "";

            var fleet = enemyFleets[CurrentIndex];
            string faction = fleet.faction?.displayName ?? "Unknown";
            int ships = fleet.ships?.Count ?? 0;
            return $"{fleet.displayName} ({faction}), {ships} ship{(ships != 1 ? "s" : "")}, {posStr}";
        }

        private string GetTrajectoryAnnouncement(string posStr)
        {
            if (Trajectories == null || CurrentIndex >= Trajectories.Length)
                return "";

            float availableDV = IsTheoreticalMode
                ? theoreticalDeltaV_mps / 1000f
                : Fleet.currentDeltaV_kps;

            return $"{trajectoryReader.ReadSummary(Trajectories[CurrentIndex], availableDV)}, {posStr}";
        }

        private string GetConfirmOptionAnnouncement(string posStr)
        {
            return CurrentIndex == 0
                ? $"Yes - Assign this transfer, {posStr}"
                : $"No - Cancel, {posStr}";
        }

        private string GetConfirmationPrompt()
        {
            if (Trajectories == null || SelectedTrajectoryIndex >= Trajectories.Length)
                return "Confirm transfer?";

            var traj = Trajectories[SelectedTrajectoryIndex];
            string dest = GetDestinationDescription();
            return $"Assign transfer to {dest}? {traj.DV_kps:F1} km/s, arrives {traj.arrivalTime?.ToCustomTimeDateString() ?? "unknown"}";
        }

        private string GetDestinationDescription()
        {
            if (SelectedOrbit != null)
                return $"{SelectedOrbit.displayName} at {SelectedBody?.displayName ?? "unknown"}";
            if (SelectedHab != null)
                return SelectedHab.displayName;
            if (TargetFleet != null)
                return $"intercept {TargetFleet.displayName}";
            return "destination";
        }

        /// <summary>
        /// Read detailed info about the current item.
        /// </summary>
        public string ReadCurrentItemDetail()
        {
            return CurrentStep switch
            {
                TransferStep.SelectOriginOrbit when originOrbits != null && CurrentIndex < originOrbits.Count
                    => orbitReader.ReadDetail(originOrbits[CurrentIndex]),
                TransferStep.SelectOrbit when orbits != null && CurrentIndex < orbits.Count
                    => orbitReader.ReadDetail(orbits[CurrentIndex]),
                TransferStep.ViewTrajectories when Trajectories != null && CurrentIndex < Trajectories.Length
                    => trajectoryReader.ReadDetail(Trajectories[CurrentIndex],
                        IsTheoreticalMode ? theoreticalDeltaV_mps / 1000f : Fleet.currentDeltaV_kps),
                _ => GetCurrentItemAnnouncement()
            };
        }

        #endregion
    }
}
