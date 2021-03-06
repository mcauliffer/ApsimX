namespace Models.PMF.Organs
{
    using APSIM.Shared.Utilities;
    using Library;
    using Models.Core;
    using Models.Interfaces;
    using Models.PMF.Functions;
    using Models.PMF.Interfaces;
    using Models.Soils;
    using Models.Soils.Arbitrator;
    using System;
    using System.Collections.Generic;
    using System.Xml.Serialization;

    ///<summary>
    /// # [Name]
    /// The generic root model calculates root growth in terms of rooting depth, biomass accumulation and subsequent root length density in each sol layer. 
    /// 
    /// **Root Growth**
    /// 
    /// Roots grow downwards through the soil profile, with initial depth determined by sowing depth and the growth rate determined by RootFrontVelocity. 
    /// The RootFrontVelocity is modified by multiplying it by the soil's XF value; which represents any resistance posed by the soil to root extension. 
    /// Root depth is also constrained by a maximum root depth.
    /// 
    /// Root length growth is calculated using the daily DM partitioned to roots and a specific root length.  Root proliferation in layers is calculated using an approach similar to the generalised equimarginal criterion used in economics.  The uptake of water and N per unit root length is used to partition new root material into layers of higher 'return on investment'.
    /// 
    /// **Dry Matter Demands**
    /// 
    /// A daily DM demand is provided to the organ abitrator and a DM supply returned. By default, 100% of the dry matter (DM) demanded from the root is structural.  
    /// The daily loss of roots is calculated using a SenescenceRate function.  All senesced material is automatically detached and added to the soil FOM.  
    /// 
    /// **Nitrogen Demands**
    /// 
    /// The daily structural N demand from root is the product of total DM demand and the minimum N concentration.  Any N above this is considered Storage 
    /// and can be used for retranslocation and/or reallocation is the respective factors are set to values other then zero.  
    /// 
    /// **Nitrogen Uptake**
    /// 
    /// Potential N uptake by the root system is calculated for each soil layer (i) that the roots have extended into.  
    /// In each layer potential uptake is calculated as the product of the mineral nitrogen in the layer, a factor controlling the rate of extraction
    /// (kNO3 or kNH4), the concentration of N form (ppm), and a soil moisture factor (NUptakeSWFactor) which typically decreases as the soil dries.  
    /// 
    ///     _NO3 uptake = NO3<sub>i</sub> x KNO3 x NO3<sub>ppm, i</sub> x NUptakeSWFactor_
    ///     
    ///     _NH4 uptake = NH4<sub>i</sub> x KNH4 x NH4<sub>ppm, i</sub> x NUptakeSWFactor_
    /// 
    /// Nitrogen uptake demand is limited to the maximum daily potential uptake (MaxDailyNUptake) and the plants N demand. 
    /// The demand for soil N is then passed to the soil arbitrator which determines how much of the N uptake demand
    /// each plant instance will be allowed to take up.
    /// 
    /// **Water Uptake**
    /// 
    /// Potential water uptake by the root system is calculated for each soil layer that the roots have extended into.  
    /// In each layer potential uptake is calculated as the product of the available water in the layer (water above LL limit) 
    /// and a factor controlling the rate of extraction (KL).  The values of both LL and KL are set in the soil interface and
    /// KL may be further modified by the crop via the KLModifier function.  
    /// 
    /// _SW uptake = (SW<sub>i</sub> - LL<sub>i</sub>) x KL<sub>i</sub> x KLModifier_
    /// 
    ///</summary>
    [Serializable]
    [Description("Root Class")]
    [ViewName("UserInterface.Views.GridView")]
    [PresenterName("UserInterface.Presenters.PropertyPresenter")]
    public class Root : Model, IWaterNitrogenUptake, IArbitration, IOrgan
    {
        /// <summary>The plant</summary>
        [Link]
        protected Plant Plant = null;

        /// <summary>The surface organic matter model</summary>
        [Link]
        public ISurfaceOrganicMatter SurfaceOrganicMatter = null;

        /// <summary>Link to biomass removal model</summary>
        [ChildLink]
        private BiomassRemoval biomassRemovalModel = null;
        
        /// <summary>The DM demand function</summary>
        [ChildLinkByName]
        [Units("g/m2/d")]
        private IFunction dmDemandFunction = null;

        /// <summary>Link to the KNO3 link</summary>
        [ChildLinkByName]
        private IFunction kno3 = null;

        /// <summary>Link to the KNH4 link</summary>
        [ChildLinkByName]
        private IFunction knh4 = null;

        /// <summary>Soil water factor for N Uptake</summary>
        [ChildLinkByName]
        private IFunction nUptakeSWFactor = null;

        /// <summary>Gets or sets the initial biomass dry matter weight</summary>
        [ChildLinkByName]
        [Units("g/plant")]
        private IFunction initialDM = null;

        /// <summary>Gets or sets the specific root length</summary>
        [ChildLinkByName]
        [Units("m/g")]
        private IFunction specificRootLength = null;

        /// <summary>The nitrogen demand switch</summary>
        [ChildLinkByName]
        private IFunction nitrogenDemandSwitch = null;

        /// <summary>The N retranslocation factor</summary>
        [ChildLinkByName(IsOptional = true)]
        [Units("/d")]
        private IFunction nRetranslocationFactor = null;

        /// <summary>The N reallocation factor</summary>
        [ChildLinkByName(IsOptional = true)]
        [Units("/d")]
        private IFunction nReallocationFactor = null;

        /// <summary>The DM retranslocation factor</summary>
        [ChildLinkByName(IsOptional = true)]
        [Units("/d")]
        private IFunction dmRetranslocationFactor = null;

        /// <summary>The DM reallocation factor</summary>
        [ChildLinkByName(IsOptional = true)]
        [Units("/d")]
        private IFunction dmReallocationFactor = null;

        /// <summary>The biomass senescence rate</summary>
        [ChildLinkByName]
        [Units("/d")]
        private IFunction senescenceRate = null;

        /// <summary>The root front velocity</summary>
        [ChildLinkByName]
        [Units("mm/d")]
        private IFunction rootFrontVelocity = null;

        /// <summary>The DM structural fraction</summary>
        [ChildLinkByName(IsOptional = true)]
        [Units("g/g")]
        private IFunction structuralFraction = null;

        /// <summary>The maximum N concentration</summary>
        [ChildLinkByName]
        [Units("g/g")]
        private IFunction maximumNConc = null;

        /// <summary>The minimum N concentration</summary>
        [ChildLinkByName]
        [Units("g/g")]
        private IFunction minimumNConc = null;

        /// <summary>The critical N concentration</summary>
        [ChildLinkByName(IsOptional = true)]
        [Units("g/g")]
        private IFunction criticalNConc = null;

        /// <summary>The maximum daily N uptake</summary>
        [ChildLinkByName]
        [Units("kg N/ha")]
        private IFunction maxDailyNUptake = null;

        /// <summary>The kl modifier</summary>
        [ChildLinkByName]
        [Units("0-1")]
        private IFunction klModifier = null;

        /// <summary>The Maximum Root Depth</summary>
        [ChildLinkByName]
        [Units("mm")]
        private IFunction maximumRootDepth = null;
        
        /// <summary>Dry matter efficiency function</summary>
        [ChildLinkByName]
        private IFunction dmConversionEfficiency = null;
        
        /// <summary>Carbon concentration</summary>
        [Units("-")]
        [ChildLinkByName]
        private IFunction carbonConcentration = null;

        /// <summary>The cost for remobilisation</summary>
        [ChildLinkByName]
        private IFunction remobilisationCost = null;

        /// <summary>The proportion of biomass respired each day</summary> 
        [ChildLinkByName]
        [Units("/d")]
        private IFunction maintenanceRespirationFunction = null;

        /// <summary>Do we need to recalculate (expensive operation) live and dead</summary>
        private bool needToRecalculateLiveDead = true;

        /// <summary>Live biomass</summary>
        private Biomass liveBiomass = new Biomass();

        /// <summary>Dead biomass</summary>
        private Biomass deadBiomass = new Biomass();

        /// <summary>The dry matter supply</summary>
        private BiomassSupplyType dryMatterSupply = new BiomassSupplyType();

        /// <summary>The nitrogen supply</summary>
        private BiomassSupplyType nitrogenSupply = new BiomassSupplyType();

        /// <summary>The dry matter demand</summary>
        private BiomassPoolType dryMatterDemand = new BiomassPoolType();

        /// <summary>Structural nitrogen demand</summary>
        private BiomassPoolType nitrogenDemand = new BiomassPoolType();

        /// <summary>The DM supply for retranslocation</summary>
        private double dmRetranslocationSupply = 0.0;

        /// <summary>The DM supply for reallocation</summary>
        private double dmMReallocationSupply = 0.0;

        /// <summary>The N supply for retranslocation</summary>
        private double nRetranslocationSupply = 0.0;

        /// <summary>The N supply for reallocation</summary>
        private double nReallocationSupply = 0.0;

        /// <summary>The structural DM demand</summary>
        private double structuralDMDemand = 0.0;

        /// <summary>The non structural DM demand</summary>
        private double storageDMDemand = 0.0;

        /// <summary>The metabolic DM demand</summary>
        private double metabolicDMDemand = 0.0;

        /// <summary>The structural N demand</summary>
        private double structuralNDemand = 0.0;

        /// <summary>The non structural N demand</summary>
        private double storageNDemand = 0.0;

        /// <summary>The metabolic N demand</summary>
        private double metabolicNDemand = 0.0;

        /// <summary>Constructor</summary>
        public Root()
        {
            Zones = new List<ZoneState>();
            ZoneNamesToGrowRootsIn = new List<string>();
            ZoneRootDepths = new List<double>();
            ZoneInitialDM = new List<double>();
        }

        /// <summary>A list of other zone names to grow roots in</summary>
        [XmlIgnore]
        public List<string> ZoneNamesToGrowRootsIn { get; set; }

        /// <summary>The root depths for each addition zone.</summary>
        [XmlIgnore]
        public List<double> ZoneRootDepths { get; set; }

        /// <summary>The live weights for each addition zone.</summary>
        [XmlIgnore]
        public List<double> ZoneInitialDM { get; set; }

        /// <summary>A list of all zones to grow roots in</summary>
        [XmlIgnore]
        public List<ZoneState> Zones { get; set; }

        /// <summary>The zone where the plant is growing</summary>
        [XmlIgnore]
        public ZoneState PlantZone { get; set; }

        /// <summary>Gets the live biomass.</summary>
        [XmlIgnore]
        [Units("g/m^2")]
        public Biomass Live
        {
            get
            {
                RecalculateLiveDead();
                return liveBiomass;
            }
        }

        /// <summary>Gets the dead biomass.</summary>
        [XmlIgnore]
        [Units("g/m^2")]
        public Biomass Dead
        {
            get
            {
                RecalculateLiveDead();
                return deadBiomass;
            }
        }

        /// <summary>Gets the root length density.</summary>
        [Units("mm/mm3")]
        public double[] LengthDensity
        {
            get
            {
                if (PlantZone == null)    // Can be null in autodoc
                    return new double[0]; 
                double[] value;
                value = new double[PlantZone.soil.Thickness.Length];
                for (int i = 0; i < PlantZone.soil.Thickness.Length; i++)
                    value[i] = PlantZone.LayerLive[i].Wt * specificRootLength.Value() * 1000 / 1000000 / PlantZone.soil.Thickness[i];
                return value;
            }
        }

        ///<Summary>Total DM demanded by roots</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double TotalDMDemand { get; set; }

        ///<Summary>The amount of N taken up after arbitration</Summary>
        [Units("g/m2")]
        [XmlIgnore]
        public double NTakenUp { get; set; }

        /// <summary>Root depth.</summary>
        [XmlIgnore]
        public double Depth { get { return PlantZone.Depth; } }

        /// <summary>Layer live</summary>
        [XmlIgnore]
        public Biomass[] LayerLive { get { return PlantZone.LayerLive; } }

        /// <summary>Layer dead.</summary>
        [XmlIgnore]
        public Biomass[] LayerDead { get { return PlantZone.LayerDead; } }

        /// <summary>Gets or sets the water uptake.</summary>
        [Units("mm")]
        public double WaterUptake
        {
            get
            {
                double uptake = 0;
                foreach (ZoneState zone in Zones)
                    uptake = uptake + MathUtilities.Sum(zone.Uptake);
                return -uptake;
            }
        }

        /// <summary>Gets or sets the water uptake.</summary>
        [Units("kg/ha")]
        public double NUptake
        {
            get
            {
                double uptake = 0;
                foreach (ZoneState zone in Zones)
                    uptake = MathUtilities.Sum(zone.NitUptake);
                return uptake;
            }
        }

        /// <summary>Gets or sets the mid points of each layer</summary>
        [XmlIgnore]
        public double[] LayerMidPointDepth { get; private set; }

        /// <summary>Gets or sets root water content</summary>
        [XmlIgnore]
        public double[] RWC { get; private set; }

        /// <summary>Gets a factor to account for root zone Water tension weighted for root mass.</summary>
        [Units("0-1")]
        public double WaterTensionFactor
        {
            get
            {
                if (PlantZone == null)
                    return 0;

                double MeanWTF = 0;

                double liveWt = Live.Wt;
                if (liveWt > 0)
                    foreach (ZoneState Z in Zones)
                    {
                        double[] paw = Z.soil.PAW;
                        double[] pawc = Z.soil.PAWC;
                        Biomass[] layerLiveForZone = Z.LayerLive;
                        for (int i = 0; i < Z.LayerLive.Length; i++)
                            MeanWTF += layerLiveForZone[i].Wt / liveWt * MathUtilities.Bound(2 * paw[i] / pawc[i], 0, 1);
                    }

                return MeanWTF;
            }
        }

        /// <summary>Gets or sets the minimum nconc.</summary>
        public double MinNconc { get { return minimumNConc.Value(); } }

        /// <summary>Gets the total biomass</summary>
        public Biomass Total { get { return Live + Dead; } }

        /// <summary>Gets the total grain weight</summary>
        [Units("g/m2")]
        public double Wt { get { return Total.Wt; } }

        /// <summary>Gets the total grain N</summary>
        [Units("g/m2")]
        public double N { get { return Total.N; } }

        /// <summary>Gets or sets the n fixation cost.</summary>
        [XmlIgnore]
        public double NFixationCost { get { return 0; } }

        /// <summary>Growth Respiration</summary>
        /// [Units("CO_2")]
        public double GrowthRespiration { get; set; }

        /// <summary>The amount of mass lost each day from maintenance respiration</summary>
        public double MaintenanceRespiration { get; set; }

        /// <summary>Gets the biomass allocated (represented actual growth)</summary>
        [XmlIgnore]
        public Biomass Allocated { get; set; }

        /// <summary>Gets the biomass senesced (transferred from live to dead material)</summary>
        [XmlIgnore]
        public Biomass Senesced { get; set; }

        /// <summary>Gets the DM amount detached (sent to soil/surface organic matter) (g/m2)</summary>
        [XmlIgnore]
        public Biomass Detached { get; set; }

        /// <summary>Gets the DM amount removed from the system (harvested, grazed, etc) (g/m2)</summary>
        [XmlIgnore]
        public Biomass Removed { get; set; }

        /// <summary>Gets the dry matter supply.</summary>
        [XmlIgnore]
        public BiomassSupplyType DMSupply { get { return dryMatterSupply; } }
        
        /// <summary>Gets dry matter demand.</summary>
        [XmlIgnore]
        public BiomassPoolType DMDemand { get { return dryMatterDemand; } }

        /// <summary>Gets the nitrogen supply.</summary>
        [XmlIgnore]
        public BiomassSupplyType NSupply { get { return nitrogenSupply; } }
        
        /// <summary>Gets the nitrogen demand.</summary>
        [XmlIgnore]
        public BiomassPoolType NDemand { get { return nitrogenDemand; } }

        /// <summary>Does the water uptake.</summary>
        /// <param name="Amount">The amount.</param>
        /// <param name="zoneName">Zone name to do water uptake in</param>
        public void DoWaterUptake(double[] Amount, string zoneName)
        {
            ZoneState zone = Zones.Find(z => z.Name == zoneName);
            if (zone == null)
                throw new Exception("Cannot find a zone called " + zoneName);

            zone.Uptake = MathUtilities.Multiply_Value(Amount, -1.0);
            zone.soil.SoilWater.dlt_sw_dep = zone.Uptake;
        }

        /// <summary>Does the Nitrogen uptake.</summary>
        /// <param name="zonesFromSoilArbitrator">List of zones from soil arbitrator</param>
        public void DoNitrogenUptake(List<ZoneWaterAndN> zonesFromSoilArbitrator)
        {
            foreach (ZoneWaterAndN thisZone in zonesFromSoilArbitrator)
            {
                ZoneState zone = Zones.Find(z => z.Name == thisZone.Zone.Name);
                if (zone != null)
                {
                    zone.solutes.Subtract("NO3", SoluteManager.SoluteSetterType.Plant, thisZone.NO3N);
                    zone.solutes.Subtract("NH4", SoluteManager.SoluteSetterType.Plant, thisZone.NH4N);

                    zone.NitUptake = MathUtilities.Multiply_Value(MathUtilities.Add(thisZone.NO3N, thisZone.NH4N), -1);
                }
            }
        }

        /// <summary>Calculate and return the dry matter supply (g/m2)</summary>
        public BiomassSupplyType CalculateDryMatterSupply()
        {
            dryMatterSupply.Fixation = 0.0;
            dryMatterSupply.Retranslocation = dmRetranslocationSupply;
            dryMatterSupply.Reallocation = dmMReallocationSupply;
            return dryMatterSupply;
        }

        /// <summary>Calculate and return the nitrogen supply (g/m2)</summary>
        public BiomassSupplyType CalculateNitrogenSupply()
        {
            nitrogenSupply.Fixation = 0.0;
            nitrogenSupply.Uptake = 0.0;
            nitrogenSupply.Retranslocation = nRetranslocationSupply;
            nitrogenSupply.Reallocation = nReallocationSupply;

            return nitrogenSupply;
        }

        /// <summary>Calculate and return the dry matter demand (g/m2)</summary>
        public BiomassPoolType CalculateDryMatterDemand()
        {
            if (Plant.SowingData.Depth < PlantZone.Depth)
            {
                structuralDMDemand = DemandedDMStructural();
                storageDMDemand = DemandedDMStorage();
                TotalDMDemand = structuralDMDemand + storageDMDemand + metabolicDMDemand;
                ////This sum is currently not necessary as demand is not calculated on a layer basis.
                //// However it might be some day... and can consider non structural too
            }

            dryMatterDemand.Structural = structuralDMDemand;
            dryMatterDemand.Storage = storageDMDemand;

            return dryMatterDemand;
        }

        /// <summary>Calculate and return the nitrogen demand (g/m2)</summary>
        public BiomassPoolType CalculateNitrogenDemand()
        {
            // This is basically the old/original function with added metabolicN.
            // Calculate N demand based on amount of N needed to bring root N content in each layer up to maximum.

            double NitrogenSwitch = (nitrogenDemandSwitch == null) ? 1.0 : nitrogenDemandSwitch.Value();
            double criticalN = (criticalNConc == null) ? minimumNConc.Value() : criticalNConc.Value();

            structuralNDemand = 0.0;
            metabolicNDemand = 0.0;
            storageNDemand = 0.0;
            foreach (ZoneState Z in Zones)
            {
                Z.StructuralNDemand = new double[Z.soil.Thickness.Length];
                Z.StorageNDemand = new double[Z.soil.Thickness.Length];
                //Note: MetabolicN is assumed to be zero

                double NDeficit = 0.0;
                for (int i = 0; i < Z.LayerLive.Length; i++)
                {
                    Z.StructuralNDemand[i] = Z.LayerLive[i].PotentialDMAllocation * minimumNConc.Value() * NitrogenSwitch;
                    NDeficit = Math.Max(0.0, maximumNConc.Value() * (Z.LayerLive[i].Wt + Z.LayerLive[i].PotentialDMAllocation) - (Z.LayerLive[i].N + Z.StructuralNDemand[i]));
                    Z.StorageNDemand[i] = Math.Max(0, NDeficit - Z.StructuralNDemand[i]) * NitrogenSwitch;

                    structuralNDemand += Z.StructuralNDemand[i];
                    storageNDemand += Z.StorageNDemand[i];
                }
            }
            nitrogenDemand.Structural = structuralNDemand;
            nitrogenDemand.Storage = storageNDemand;
            nitrogenDemand.Metabolic = metabolicNDemand;
            return nitrogenDemand;
        }

        /// <summary>Sets the dry matter potential allocation.</summary>
        public void SetDryMatterPotentialAllocation(BiomassPoolType dryMatter)
        {
            if (PlantZone.Uptake == null)
                throw new Exception("No water and N uptakes supplied to root. Is Soil Arbitrator included in the simulation?");

            if (PlantZone.Depth <= 0)
                return; //cannot allocate growth where no length

            if (dryMatterDemand.Structural == 0 && dryMatter.Structural > 0.000000000001)
                throw new Exception("Invalid allocation of potential DM in" + Name);


            double TotalRAw = 0;
            foreach (ZoneState Z in Zones)
                TotalRAw += MathUtilities.Sum(Z.CalculateRootActivityValues());

            if (TotalRAw == 0 && dryMatter.Structural > 0)
                throw new Exception("Error trying to partition potential root biomass");

            if (TotalRAw > 0)
            {
                foreach (ZoneState Z in Zones)
                {
                    double[] RAw = Z.CalculateRootActivityValues();
                    for (int layer = 0; layer < Z.soil.Thickness.Length; layer++)
                        Z.LayerLive[layer].PotentialDMAllocation = dryMatter.Structural * RAw[layer] / TotalRAw;
                }
                needToRecalculateLiveDead = true;
            }
        }

        /// <summary>Sets the dry matter allocation.</summary>
        public void SetDryMatterAllocation(BiomassAllocationType dryMatter)
        {
            double TotalRAw = 0;
            foreach (ZoneState Z in Zones)
                TotalRAw += MathUtilities.Sum(Z.CalculateRootActivityValues());

            Allocated.StructuralWt = dryMatter.Structural * dmConversionEfficiency.Value();
            Allocated.StorageWt = dryMatter.Storage * dmConversionEfficiency.Value();
            Allocated.MetabolicWt = dryMatter.Metabolic * dmConversionEfficiency.Value();
            // GrowthRespiration with unit CO2 
            // GrowthRespiration is calculated as 
            // Allocated CH2O from photosynthesis "1 / DMConversionEfficiency.Value()", converted 
            // into carbon through (12 / 30), then minus the carbon in the biomass, finally converted into 
            // CO2 (44/12).
            double growthRespFactor = ((1.0 / dmConversionEfficiency.Value()) * (12.0 / 30.0) - 1.0 * carbonConcentration.Value()) * 44.0 / 12.0;
            GrowthRespiration = (Allocated.StructuralWt + Allocated.StorageWt + Allocated.MetabolicWt) * growthRespFactor;
            if (TotalRAw == 0 && Allocated.Wt > 0)
                throw new Exception("Error trying to partition root biomass");

            foreach (ZoneState Z in Zones)
                Z.PartitionRootMass(TotalRAw, Allocated.Wt);
            needToRecalculateLiveDead = true;
        }

        /// <summary>Gets the nitrogen supply from the specified zone.</summary>
        /// <param name="zone">The zone.</param>
        /// <param name="NO3Supply">The returned NO3 supply</param>
        /// <param name="NH4Supply">The returned NH4 supply</param>
        public void CalculateNitrogenSupply(ZoneWaterAndN zone, ref double[] NO3Supply, ref double[] NH4Supply)
        {

            ZoneState myZone = Zones.Find(z => z.Name == zone.Zone.Name);
            if (myZone != null)
            {
                if (RWC == null || RWC.Length != myZone.soil.Thickness.Length)
                    RWC = new double[myZone.soil.Thickness.Length];
                double NO3Uptake = 0;
                double NH4Uptake = 0;

                double[] thickness = myZone.soil.Thickness;
                double[] water = myZone.soil.Water;
                double[] ll15mm = myZone.soil.LL15mm;
                double[] dulmm = myZone.soil.DULmm;
                double[] bd = myZone.soil.BD;

                double accuDepth = 0;
                
                for (int layer = 0; layer < thickness.Length; layer++)
                {
                    accuDepth += thickness[layer];
                    if (myZone.LayerLive[layer].Wt > 0)
                    {
                        double factorRootDepth = Math.Max(0, Math.Min(1, 1 - (accuDepth - Depth ) / thickness[layer]));
                        RWC[layer] = (water[layer] - ll15mm[layer]) / (dulmm[layer] - ll15mm[layer]);
                        RWC[layer] = Math.Max(0.0, Math.Min(RWC[layer], 1.0));
                        double SWAF = nUptakeSWFactor.Value(layer);

                        double kno3 = this.kno3.Value(layer);
                        double NO3ppm = zone.NO3N[layer] * (100.0 / (bd[layer] * thickness[layer]));
                        NO3Supply[layer] = Math.Min(zone.NO3N[layer] * kno3 * NO3ppm * SWAF * factorRootDepth, (maxDailyNUptake.Value() - NO3Uptake));
                        NO3Uptake += NO3Supply[layer];

                        double knh4 = this.knh4.Value(layer);
                        double NH4ppm = zone.NH4N[layer] * (100.0 / (bd[layer] * thickness[layer]));
                        NH4Supply[layer] = Math.Min(zone.NH4N[layer] * knh4 * NH4ppm * SWAF * factorRootDepth, (maxDailyNUptake.Value() - NH4Uptake));
                        NH4Uptake += NH4Supply[layer];
                    }
                }
            }
        }

        /// <summary>Sets the n allocation.</summary>
        public void SetNitrogenAllocation(BiomassAllocationType nitrogen)
        {
            double totalStructuralNDemand = 0;
            double totalNDemand = 0;

            foreach (ZoneState Z in Zones)
            {
                totalStructuralNDemand += MathUtilities.Sum(Z.StructuralNDemand);
                totalNDemand += MathUtilities.Sum(Z.StructuralNDemand) + MathUtilities.Sum(Z.StorageNDemand);
            }
            NTakenUp = nitrogen.Uptake;
            Allocated.StructuralN = nitrogen.Structural;
            Allocated.StorageN = nitrogen.Storage;
            Allocated.MetabolicN = nitrogen.Metabolic;

            double surplus = Allocated.N - totalNDemand;
            if (surplus > 0.000000001)
                throw new Exception("N Allocation to roots exceeds Demand");
            double NAllocated = 0;

            foreach (ZoneState Z in Zones)
            {
                for (int i = 0; i < Z.LayerLive.Length; i++)
                {
                    if (totalStructuralNDemand > 0)
                    {
                        double StructFrac = Z.StructuralNDemand[i] / totalStructuralNDemand;
                        Z.LayerLive[i].StructuralN += nitrogen.Structural * StructFrac;
                        NAllocated += nitrogen.Structural * StructFrac;
                    }
                    double totalStorageNDemand = MathUtilities.Sum(Z.StorageNDemand);
                    if (totalStorageNDemand > 0)
                    {
                        double NonStructFrac = Z.StorageNDemand[i] / totalStorageNDemand;
                        Z.LayerLive[i].StorageN += nitrogen.Storage * NonStructFrac;
                        NAllocated += nitrogen.Storage * NonStructFrac;
                    }
                }
            }
            needToRecalculateLiveDead = true;

            if (!MathUtilities.FloatsAreEqual(NAllocated - Allocated.N, 0.0))
                throw new Exception("Error in N Allocation: " + Name);
        }

        /// <summary>Gets or sets the water supply.</summary>
        /// <param name="zone">The zone.</param>
        public double[] CalculateWaterSupply(ZoneWaterAndN zone)
        {
            ZoneState myZone = Zones.Find(z => z.Name == zone.Zone.Name);
            if (myZone == null)
                return null;

            if (myZone.soil.Weirdo != null)
                return new double[myZone.soil.Thickness.Length]; //With Weirdo, water extraction is not done through the arbitrator because the time step is different.
            else
            {
                double[] kl = myZone.soil.KL(Plant.Name);
                double[] ll = myZone.soil.LL(Plant.Name);

                double[] supply = new double[myZone.soil.Thickness.Length];
                LayerMidPointDepth = Soil.ToMidPoints(myZone.soil.Thickness);
                for (int layer = 0; layer < myZone.soil.Thickness.Length; layer++)
                {
                    if (layer <= Soil.LayerIndexOfDepth(myZone.Depth, myZone.soil.Thickness))
                    {
                        supply[layer] = Math.Max(0.0, kl[layer] * klModifier.Value(layer) *
                            (zone.Water[layer] - ll[layer] * myZone.soil.Thickness[layer]) * Soil.ProportionThroughLayer(layer, myZone.Depth, myZone.soil.Thickness));
                    }
                }
                return supply;
            }            
        }

        /// <summary>Removes biomass from root layers when harvest, graze or cut events are called.</summary>
        /// <param name="biomassRemoveType">Name of event that triggered this biomass remove call.</param>
        /// <param name="removal">The fractions of biomass to remove</param>
        public void DoRemoveBiomass(string biomassRemoveType, OrganBiomassRemovalType removal)
        {
            biomassRemovalModel.RemoveBiomassToSoil(biomassRemoveType, removal, PlantZone.LayerLive, PlantZone.LayerDead, Removed, Detached);
        }

        /// <summary>Initialise all zones.</summary>
        private void InitialiseZones()
        {
            Zones.Clear();
            Zones.Add(PlantZone);
            if (ZoneRootDepths.Count != ZoneNamesToGrowRootsIn.Count ||
                ZoneRootDepths.Count != ZoneInitialDM.Count)
                throw new Exception("The root zone variables (ZoneRootDepths, ZoneNamesToGrowRootsIn, ZoneInitialDM) need to have the same number of values");

            for (int i = 0; i < ZoneNamesToGrowRootsIn.Count; i++)
            {
                Zone zone = Apsim.Find(this, ZoneNamesToGrowRootsIn[i]) as Zone;
                if (zone != null)
                {
                    Soil soil = Apsim.Find(zone, typeof(Soil)) as Soil;
                    if (soil == null)
                        throw new Exception("Cannot find soil in zone: " + zone.Name);
                    if (soil.Crop(Plant.Name) == null)
                        throw new Exception("Cannot find a soil crop parameterisation for " + Plant.Name);
                    ZoneState newZone = new ZoneState(Plant, this, soil, ZoneRootDepths[i], ZoneInitialDM[i], Plant.Population, maximumNConc.Value(),
                                                      rootFrontVelocity, maximumRootDepth, remobilisationCost);
                    Zones.Add(newZone);
                }
            }
            needToRecalculateLiveDead = true;
        }

        /// <summary>Clears this instance.</summary>
        private void Clear()
        {
            Live.Clear();
            Dead.Clear();
            PlantZone.Clear();
            Zones.Clear();
            needToRecalculateLiveDead = true;
        }

        /// <summary>Recalculate live and dead biomass if necessary</summary>
        private void RecalculateLiveDead()
        {
            if (needToRecalculateLiveDead)
            {
                needToRecalculateLiveDead = false;
                liveBiomass.Clear();
                deadBiomass.Clear();
                foreach (Biomass b in PlantZone.LayerLive)
                    liveBiomass.Add(b);
                foreach (Biomass b in PlantZone.LayerDead)
                    deadBiomass.Add(b);
            }
        }

        /// <summary>Computes the DM and N amounts that are made available for new growth</summary>
        private void DoSupplyCalculations()
        {
            dmMReallocationSupply = AvailableDMReallocation();
            dmRetranslocationSupply = AvailableDMRetranslocation();
            nReallocationSupply = AvailableNReallocation();
            nRetranslocationSupply = AvailableNRetranslocation();
        }

        /// <summary>Computes the amount of DM available for reallocation.</summary>
        private double AvailableDMReallocation()
        {
            if (dmReallocationFactor != null)
            {
                double rootLiveStorageWt = 0.0;
                foreach (ZoneState Z in Zones)
                    for (int i = 0; i < Z.LayerLive.Length; i++)
                        rootLiveStorageWt += Z.LayerLive[i].StorageWt;

                double availableDM = rootLiveStorageWt * senescenceRate.Value() * dmReallocationFactor.Value();
                if (availableDM < -Util.BiomassToleranceValue)
                    throw new Exception("Negative DM reallocation value computed for " + Name);
                return availableDM;
            }
            // By default reallocation is turned off!!!!
            return 0.0;
        }

        /// <summary>Computes the N amount available for retranslocation.</summary>
        /// <remarks>This is limited to ensure Nconc does not go below MinimumNConc</remarks>
        private double AvailableNRetranslocation()
        {
            if (nRetranslocationFactor != null)
            {
                double labileN = 0.0;
                foreach (ZoneState Z in Zones)
                    for (int i = 0; i < Z.LayerLive.Length; i++)
                        labileN += Math.Max(0.0, Z.LayerLive[i].StorageN - Z.LayerLive[i].StorageWt * minimumNConc.Value());

                double availableN = Math.Max(0.0, labileN - nReallocationSupply) * nRetranslocationFactor.Value();
                if (availableN < -Util.BiomassToleranceValue)
                    throw new Exception("Negative N retranslocation value computed for " + Name);

                return availableN;
            }
            else
            {  // By default retranslocation is turned off!!!!
                return 0.0;
            }
        }

        /// <summary>Computes the N amount available for reallocation.</summary>
        private double AvailableNReallocation()
        {
            if (nReallocationFactor != null)
            {
                double rootLiveStorageN = 0.0;
                foreach (ZoneState Z in Zones)
                    for (int i = 0; i < Z.LayerLive.Length; i++)
                        rootLiveStorageN += Z.LayerLive[i].StorageN;

                double availableN = rootLiveStorageN * senescenceRate.Value() * nReallocationFactor.Value();
                if (availableN < -Util.BiomassToleranceValue)
                    throw new Exception("Negative N reallocation value computed for " + Name);

                return availableN;
            }
            else
            {  // By default reallocation is turned off!!!!
                return 0.0;
            }
        }

        /// <summary>Computes the amount of structural DM demanded.</summary>
        private double DemandedDMStructural()
        {
            if (dmConversionEfficiency.Value() > 0.0)
            {
                double demandedDM = dmDemandFunction.Value();
                if (structuralFraction != null)
                    demandedDM *= structuralFraction.Value() / dmConversionEfficiency.Value();
                else
                    demandedDM /= dmConversionEfficiency.Value();

                return demandedDM;
            }
            // Conversion efficiency is zero!!!!
            return 0.0;
        }

        /// <summary>Computes the amount of non structural DM demanded.</summary>
        private double DemandedDMStorage()
        {
            if ((dmConversionEfficiency.Value() > 0.0) && (structuralFraction != null))
            {
                double rootLiveStructuralWt = 0.0;
                double rootLiveStorageWt = 0.0;
                foreach (ZoneState Z in Zones)
                    for (int i = 0; i < Z.LayerLive.Length; i++)
                    {
                        rootLiveStructuralWt += Z.LayerLive[i].StructuralWt;
                        rootLiveStorageWt += Z.LayerLive[i].StorageWt;
                    }

                double theoreticalMaximumDM = (rootLiveStructuralWt + structuralDMDemand) / structuralFraction.Value();
                double baseAllocated = rootLiveStructuralWt + rootLiveStorageWt + structuralDMDemand;
                double demandedDM = Math.Max(0.0, theoreticalMaximumDM - baseAllocated) / dmConversionEfficiency.Value();
                return demandedDM;
            }
            // Either there is no Storage fraction or conversion efficiency is zero!!!!
            return 0.0;
        }

        /// <summary>Computes the amount of DM available for retranslocation.</summary>
        private double AvailableDMRetranslocation()
        {
            if (dmRetranslocationFactor != null)
            {
                double rootLiveStorageWt = 0.0;
                foreach (ZoneState Z in Zones)
                    for (int i = 0; i < Z.LayerLive.Length; i++)
                        rootLiveStorageWt += Z.LayerLive[i].StorageWt;

                double availableDM = Math.Max(0.0, rootLiveStorageWt - dmMReallocationSupply) * dmRetranslocationFactor.Value();
                if (availableDM < -Util.BiomassToleranceValue)
                    throw new Exception("Negative DM retranslocation value computed for " + Name);

                return availableDM;
            }
            else
            { // By default retranslocation is turned off!!!!
                return 0.0;
            }
        }

        /// <summary>Called when crop is ending</summary>
        [EventSubscribe("PlantEnding")]
        private void DoPlantEnding(object sender, EventArgs e)
        {
            //Send all root biomass to soil FOM
            DoRemoveBiomass(null, new OrganBiomassRemovalType() { FractionLiveToResidue = 1.0 });
            Clear();
        }

        /// <summary>Called when [simulation commencing].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        /// <exception cref="ApsimXException">Cannot find a soil crop parameterisation for  + Name</exception>
        [EventSubscribe("Commencing")]
        private void OnSimulationCommencing(object sender, EventArgs e)
        {
            Soil soil = Apsim.Find(this, typeof(Soil)) as Soil;
            if (soil == null)
                throw new Exception("Cannot find soil");
            if (soil.Crop(Plant.Name) == null && soil.Weirdo == null)
                throw new Exception("Cannot find a soil crop parameterisation for " + Plant.Name);

            PlantZone = new ZoneState(Plant, this, soil, 0, initialDM.Value(), Plant.Population, maximumNConc.Value(),
                                      rootFrontVelocity, maximumRootDepth, remobilisationCost);
            Zones = new List<ZoneState>();
            Allocated = new PMF.Biomass();
            Senesced = new Biomass();
            Detached = new Biomass();
            Removed = new Biomass();
        }

        /// <summary>Called when [do daily initialisation].</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoDailyInitialisation")]
        private void OnDoDailyInitialisation(object sender, EventArgs e)
        {
            if (Plant.IsAlive)
            {
                Allocated = new PMF.Biomass();
                Senesced = new Biomass();
                Detached = new Biomass();
                Removed = new Biomass();
            }
        }

        /// <summary>Called when crop is sown</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="data">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("PlantSowing")]
        private void OnPlantSowing(object sender, SowPlant2Type data)
        {
            if (data.Plant == Plant)
            {
                PlantZone.Initialise(Plant.SowingData.Depth, initialDM.Value(), Plant.Population, maximumNConc.Value());
                InitialiseZones();
                needToRecalculateLiveDead = true;
            }
        }

        /// <summary>Event from sequencer telling us to do our potential growth.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoPotentialPlantGrowth")]
        private void OnDoPotentialPlantGrowth(object sender, EventArgs e)
        {
            if (Plant.IsEmerged)
            {
                DoSupplyCalculations(); //TODO: This should be called from the Arbitrator, OnDoPotentialPlantPartioning
            }
        }

        /// <summary>Does the nutrient allocations.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("DoActualPlantGrowth")]
        private void OnDoActualPlantGrowth(object sender, EventArgs e)
        {
            if (Plant.IsAlive)
            {
                foreach (ZoneState Z in Zones)
                    Z.GrowRootDepth();
                // Do Root Senescence
                DoRemoveBiomass(null, new OrganBiomassRemovalType() { FractionLiveToResidue = senescenceRate.Value() });
            }
            needToRecalculateLiveDead = false;
            // Do maintenance respiration
            MaintenanceRespiration = 0;
            MaintenanceRespiration += Live.MetabolicWt * maintenanceRespirationFunction.Value();
            Live.MetabolicWt *= (1 - maintenanceRespirationFunction.Value());
            MaintenanceRespiration += Live.StorageWt * maintenanceRespirationFunction.Value();
            Live.StorageWt *= (1 - maintenanceRespirationFunction.Value());
            needToRecalculateLiveDead = true;
        }

        /// <summary>Called when crop is ending</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        [EventSubscribe("PlantEnding")]
        private void OnPlantEnding(object sender, EventArgs e)
        {
            Biomass total = Live + Dead;

            if (total.Wt > 0.0)
            {
                Detached.Add(Live);
                Detached.Add(Dead);
                SurfaceOrganicMatter.Add(total.Wt * 10, total.N * 10, 0, Plant.CropType, Name);
            }
            Clear();
        }

    }
}