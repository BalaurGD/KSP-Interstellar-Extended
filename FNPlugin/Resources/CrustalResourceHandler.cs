﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FNPlugin.Resources
{
    class CrustalResourceHandler
    {
        protected static Dictionary<int, List<CrustalResource>> body_Crustal_resource_list = new Dictionary<int, List<CrustalResource>>();
        protected static Dictionary<string, List<CrustalResource>> body_Crustal_resource_by_name = new Dictionary<string, List<CrustalResource>>();

        public static double GetCrustalResourceContent(int refBody, string resourcename)
        {
            List<CrustalResource> bodyCrustalComposition = GetCrustalCompositionForBody(refBody);

            CrustalResource resource = bodyCrustalComposition.FirstOrDefault(oor => oor.ResourceName == resourcename);

            return resource?.ResourceAbundance ?? 0;
        }

        public static double GetCrustalResourceContent(int refBody, int resource)
        {
            List<CrustalResource> bodyCrustalComposition = GetCrustalCompositionForBody(refBody);
            if (bodyCrustalComposition.Count > resource) return bodyCrustalComposition[resource].ResourceAbundance;
            return 0;
        }

        public static string GetCrustalResourceName(int refBody, int resource)
        {
            List<CrustalResource> bodyCrustalComposition = GetCrustalCompositionForBody(refBody);
            if (bodyCrustalComposition.Count > resource)
            {
                return bodyCrustalComposition[resource].ResourceName;
            }
            return null;
        }

        public static string GetCrustalResourceDisplayName(int refBody, int resource)
        {
            List<CrustalResource> bodyCrustalComposition = GetCrustalCompositionForBody(refBody);
            if (bodyCrustalComposition.Count > resource)
            {
                return bodyCrustalComposition[resource].DisplayName;
            }
            return null;
        }

        public static List<CrustalResource> GetCrustalCompositionForBody(CelestialBody celestialBody) // getter that uses celestial body as an argument
        {
            return GetCrustalCompositionForBody(celestialBody.flightGlobalsIndex); // calls the function that uses refBody int as an argument
        }

        public static List<CrustalResource> GetCrustalCompositionForBody(string celestrialBodyName) // function for getting or creating Crustal composition
        {
            if (body_Crustal_resource_by_name.TryGetValue(celestrialBodyName, out var bodyCrustalComposition))
                return bodyCrustalComposition;

            // try to find celestrial body id it exists in this universe
            CelestialBody celestialBody = FlightGlobals.Bodies.FirstOrDefault(m => m.name == celestrialBodyName);

            if (celestialBody != null)
            {
                // create composition from kspi Crustal definition file
                bodyCrustalComposition = CreateFromKspiCrustDefinitionFile(celestialBody);

                // add from stock resource definitions if missing
                GenerateCompositionFromResourceAbundances(celestialBody.flightGlobalsIndex, bodyCrustalComposition);

                // add missing stock resources
                AddMissingStockResources(celestialBody.flightGlobalsIndex, bodyCrustalComposition);

                // add to database for future reference
                body_Crustal_resource_list.Add(celestialBody.flightGlobalsIndex, bodyCrustalComposition);
            }
            else
            {
                // create composition from kspi Crustal definition file
                bodyCrustalComposition = CreateFromKspiCrustDefinitionFile(celestrialBodyName);
            }

            // Add rare and isotopic resources
            AddRaresAndIsotopesToCrustComposition(bodyCrustalComposition);

            if (celestialBody != null)
            {
                // add to database for future reference
                body_Crustal_resource_by_name.Add(celestialBody.name, bodyCrustalComposition);
            }

            return bodyCrustalComposition;
        }

        public static List<CrustalResource> GetCrustalCompositionForBody(int refBody) // function for getting or creating Crustal composition
        {
            var bodyCrustalComposition = new List<CrustalResource>(); // create an object list for holding all the resources
            try
            {
                // check if there's a composition for this body
                if (!body_Crustal_resource_list.TryGetValue(refBody, out bodyCrustalComposition))
                {
                    CelestialBody celestialBody = FlightGlobals.Bodies[refBody]; // create a celestialBody object referencing the current body (makes it easier on us in the next lines)

                    // create composition from kspi Crustal definition file
                    bodyCrustalComposition = CreateFromKspiCrustDefinitionFile(celestialBody);

                    // add from stock resource definitions if missing
                    GenerateCompositionFromResourceAbundances(refBody, bodyCrustalComposition); // calls the generating function below

                    // if no Crustal resource definition is created, create one based on celestialBody characteristics
                    if (bodyCrustalComposition.Sum(m => m.ResourceAbundance) < 0.5)
                    {
                        bodyCrustalComposition = GenerateCompositionFromCelestialBody(celestialBody);
                    }

                    // Add rare and isotopic resources
                    AddRaresAndIsotopesToCrustComposition(bodyCrustalComposition);

                    // add missing stock resources
                    AddMissingStockResources(refBody, bodyCrustalComposition);

                    // sort on resource abundance
                    bodyCrustalComposition = bodyCrustalComposition.OrderByDescending(bacd => bacd.ResourceAbundance).ToList();

                    // add to database for future reference
                    body_Crustal_resource_list.Add(refBody, bodyCrustalComposition);
                    body_Crustal_resource_by_name.Add(celestialBody.name, bodyCrustalComposition);
                }
            }
            catch (Exception ex)
            {
                Debug.Log("[KSPI]: Exception while loading Crustal resources : " + ex.ToString());
            }
            return bodyCrustalComposition;
        }

        private static List<CrustalResource> CreateFromKspiCrustDefinitionFile(CelestialBody celestialBody)
        {
            var bodyCrustalComposition = new List<CrustalResource>();
            //CRUSTAL_RESOURCE_PACK_DEFINITION_KSPI
            ConfigNode crustalResourcePack = GameDatabase.Instance.GetConfigNodes("CRUSTAL_RESOURCE_PACK_DEFINITION_KSPI").FirstOrDefault();

            //Debug.Log("[KSPI]: Loading Crustal data from pack: " + (Crustal_resource_pack.HasValue("name") ? Crustal_resource_pack.GetValue("name") : "unknown pack"));
            if (crustalResourcePack == null)
                return bodyCrustalComposition;

            Debug.Log("[KSPI]: searching for crustal definition for " + celestialBody.name);
            List<ConfigNode> crustalResourceList = crustalResourcePack.nodes.Cast<ConfigNode>().Where(res => res.GetValue("celestialBodyName") == celestialBody.name).ToList();
            if (crustalResourceList.Any())
            {
                bodyCrustalComposition = crustalResourceList.Select(orsc =>
                    new CrustalResource(orsc.HasValue("resourceName")
                            ? orsc.GetValue("resourceName") : null,
                        double.Parse(orsc.GetValue("abundance")),
                        orsc.GetValue("guiName"))).ToList();
            }
            return bodyCrustalComposition;
        }

        private static List<CrustalResource> CreateFromKspiCrustDefinitionFile(string celestrialBodyName)
        {
            var bodyCrustalComposition = new List<CrustalResource>();
            //CRUSTAL_RESOURCE_PACK_DEFINITION_KSPI
            ConfigNode crustalResourcePack = GameDatabase.Instance.GetConfigNodes("CRUSTAL_RESOURCE_PACK_DEFINITION_KSPI").FirstOrDefault();

            //Debug.Log("[KSPI]: Loading Crustal data from pack: " + (Crustal_resource_pack.HasValue("name") ? Crustal_resource_pack.GetValue("name") : "unknown pack"));
            if (crustalResourcePack == null)
                return bodyCrustalComposition;

            Debug.Log("[KSPI]: searching for crustal definition for " + celestrialBodyName);
            List<ConfigNode> crustalResourceList = crustalResourcePack.nodes.Cast<ConfigNode>().Where(res => res.GetValue("celestialBodyName") == celestrialBodyName).ToList();
            if (crustalResourceList.Any())
            {
                bodyCrustalComposition = crustalResourceList.Select(orsc => new CrustalResource(orsc.HasValue("resourceName") ? orsc.GetValue("resourceName") : null, double.Parse(orsc.GetValue("abundance")), orsc.GetValue("guiName"))).ToList();
            }
            return bodyCrustalComposition;
        }

        public static List<CrustalResource> GenerateCompositionFromCelestialBody(CelestialBody celestialBody) // generates Crustal composition based on Crustal characteristics
        {
            var bodyCrustalComposition = new List<CrustalResource>(); // instantiate a new list that this function will be returning

            try
            {
                // return empty if there's no crust
                if (!celestialBody.hasSolidSurface)
                    return bodyCrustalComposition;

                // Lookup homeworld
                CelestialBody homeworld = FlightGlobals.Bodies.SingleOrDefault(b => b.isHomeWorld);

                if (homeworld == null)
                    return new List<CrustalResource>();

                double pressureAtSurface = celestialBody.GetPressure(0);

                if (celestialBody.Mass < homeworld.Mass * 10 && pressureAtSurface < 1000)
                {
                    if (pressureAtSurface > 200)
                    {
                        // it is Venus-like/Eve-like, use Eve as a template
                        bodyCrustalComposition = GetCrustalCompositionForBody("Eve");
                    }
                    else if (celestialBody.Mass > (homeworld.Mass / 2) && celestialBody.Mass < homeworld.Mass && pressureAtSurface < 100) // it's at least half as big as the homeworld and has significant atmosphere
                    {
                        // it is Laythe-like, use Laythe as a template
                        bodyCrustalComposition = GetCrustalCompositionForBody("Laythe");
                    }
                    else if (celestialBody.atmosphereContainsOxygen)
                    {
                        // it is Earth-like, use Earth as a template
                        bool hasEarth = FlightGlobals.Bodies.Any(b => b.name == "Earth"); // is there a planet called Earth present in KSP?

                        bodyCrustalComposition = GetCrustalCompositionForBody(hasEarth ? "Earth" : "Kerbin");
                    }
                    else
                    {
                        // it is a Mars-like, use Mars as template
                        bool hasMars = FlightGlobals.Bodies.Any(b => b.name == "Mars"); // is there a planet called Mars present in KSP?

                        bodyCrustalComposition = GetCrustalCompositionForBody(hasMars ? "Mars" : "Duna");
                    }
                }

            }
            catch (Exception ex)
            {
                Debug.LogError("[KSPI]: Exception while generating Crustal resource composition from celestial properties : " + ex.ToString());
            }

            return bodyCrustalComposition;
        }

        public static List<CrustalResource> GenerateCompositionFromResourceAbundances(int refBody, List<CrustalResource> bodyCrustalComposition)
        {
            try
            {
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.WaterPure, "LqdWater", "H2O", "Water", "Water");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.WaterHeavy, "DeuteriumWater", "D2O", "HeavyWater", "HeavyWater");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.WaterPure, "LqdNitrogen", "NitrogenGas", "Nitrogen", "Nitrogen");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.OxygenGas, "LqdOxygen", "OxygenGas", "Oxygen", "Oxygen");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.CarbonDioxideLqd, "LqdCO2", "CO2", "CarbonDioxide", "CarbonDioxide");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.CarbonMonoxideGas, "LqdCO", "CO", "CarbonMonoxide", "CarbonMonoxide");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.MethaneLqd, "LqdMethane", "MethaneGas", "Methane", "Methane");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.ArgonLqd, "LqdArgon", "ArgonGas", "Argon", "Argon");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.DeuteriumLqd, "LqdDeuterium", "DeuteriumGas", "Deuterium", "Deuterium");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.NeonLqd, "LqdNeon", "NeonGas", "Neon", "Neon");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.XenonGas, "LqdXenon", "XenonGas", "Xenon", "Xenon");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.KryptonGas, "LqdKrypton", "KryptonGas", "Krypton", "Krypton");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.Sodium, "LqdSodium", "SodiumGas", "Sodium", "Sodium");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.UraniumNitride, "UraniumNitride", "UN", "UraniumNitride", "UraniumNitride");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.UraniumTetraflouride, "UraniumTetrafluoride", "UraniumTerraFloride", "UF4", "UF");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.Lithium6, "Lithium", "Lithium6", "Lithium-6", "LI");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.Lithium7, "Lithium7", "Lithium-7", "LI7", "Li7");
                AddResource(refBody, bodyCrustalComposition, ResourceSettings.Config.Plutonium238, "Plutionium", "Blutonium", "PU", "PU238");

                AddResource(ResourceSettings.Config.Helium4Lqd, "Helium-4", refBody, bodyCrustalComposition, new[] { "LqdHe4", "Helium4Gas", "Helium4", "Helium-4", "He4Gas", "He4", "LqdHelium", "Helium", "HeliumGas" });
                AddResource(ResourceSettings.Config.Helium3Lqd, "Helium-3", refBody, bodyCrustalComposition, new[] { "LqdHe3", "Helium3Gas", "Helium3", "Helium-3", "He3Gas", "He3" });
                AddResource(ResourceSettings.Config.HydrogenLqd, "Hydrogen", refBody, bodyCrustalComposition, new[] { "LqdHydrogen", "HydrogenGas", "Hydrogen", "H2", "Protium" });
            }
            catch (Exception ex)
            {
                Debug.LogError("[KSPI]: Exception while generating Crustal composition from defined abundances : " + ex.ToString());
            }

            return bodyCrustalComposition;
        }

        private static void AddMissingStockResources(int refBody, List<CrustalResource> bodyCrustalComposition)
        {
            // fetch all Crustal resources
            var allCrustalResources = ResourceMap.Instance.FetchAllResourceNames(HarvestTypes.Planetary);

            Debug.Log("[KSPI]: AddMissingStockResources : found " + allCrustalResources.Count + " resources");

            foreach (var resoureName in allCrustalResources)
            {
                // add resource if missing
                AddMissingResource(resoureName, refBody, bodyCrustalComposition);
            }
        }

        private static void AddMissingResource(string resourname, int refBody, List<CrustalResource> bodyCrustalComposition)
        {
            if (resourname == ResourceSettings.Config.Regolith)
            {
                Debug.Log("[KSPI]: AddMissingResource : Ignored Regolith");
                return;
            }

            // verify it is a defined resource
            PartResourceDefinition definition = PartResourceLibrary.Instance.GetDefinition(resourname);
            if (definition == null)
            {
                Debug.LogWarning("[KSPI]: AddMissingResource : Failed to find resource definition for '" + resourname + "'");
                return;
            }

            // skip it already registred or used as a Synonym
            if (bodyCrustalComposition.Any(m => m.ResourceName == definition.name || m.DisplayName == definition.displayName || m.Synonyms.Contains(definition.name)))
            {
                Debug.Log("[KSPI]: AddMissingResource : Already found existing composition for '" + resourname + "'");
                return;
            }

            // retrieve abundance
            var abundance = GetAbundance(definition.name, refBody);
            if (abundance <= 0)
            {
                Debug.LogWarning("[KSPI]: AddMissingResource : Abundance for resource '" + resourname + "' was " + abundance);
            }

            // create Crustalresource from definition and abundance
            var CrustalResource = new CrustalResource(definition, abundance);

            // add to Crustal composition
            Debug.Log("[KSPI]: AddMissingResource : add resource '" + resourname + "'");
            bodyCrustalComposition.Add(CrustalResource);
        }

        private static void AddResource(string outputResourname, string displayname, int refBody, List<CrustalResource> bodyCrustalComposition, string[] variants)
        {
            var abundances = new[] { GetAbundance(outputResourname, refBody) }.Concat(variants.Select(m => GetAbundance(m, refBody)));

            var crustalResource = new CrustalResource(outputResourname, abundances.Max(), displayname, variants);

            if (!(crustalResource.ResourceAbundance > 0))
                return;

            var existingResource = bodyCrustalComposition.FirstOrDefault(a => a.ResourceName == outputResourname);
            if (existingResource != null)
            {
                Debug.Log("[KSPI]: replaced resource " + outputResourname + " with stock defined abundance " + crustalResource.ResourceAbundance);
                bodyCrustalComposition.Remove(existingResource);
            }
            bodyCrustalComposition.Add(crustalResource);
        }

        private static void AddResource(int refBody, List<CrustalResource> bodyCrustalComposition, string outputResourname, string inputResource1, string inputResource2, string inputResource3, string displayname)
        {
            var abundances = new[] { GetAbundance(inputResource1, refBody), GetAbundance(inputResource2, refBody), GetAbundance(inputResource2, refBody) };

            var crustalResource = new CrustalResource(outputResourname, abundances.Max(), displayname, new[] { inputResource1, inputResource2, inputResource3 });

            if (!(crustalResource.ResourceAbundance > 0))
                return;

            var existingResource = bodyCrustalComposition.FirstOrDefault(a => a.ResourceName == outputResourname);
            if (existingResource != null)
            {
                Debug.Log("[KSPI]: replaced resource " + outputResourname + " with stock defined abundance " + crustalResource.ResourceAbundance);
                bodyCrustalComposition.Remove(existingResource);
            }
            bodyCrustalComposition.Add(crustalResource);
        }

        private static void AddRaresAndIsotopesToCrustComposition(List<CrustalResource> bodyCrustalComposition)
        {
            // add heavywater based on water abundance in crust
            if (bodyCrustalComposition.All(m => m.ResourceName != ResourceSettings.Config.WaterHeavy)
                && bodyCrustalComposition.Any(m => m.ResourceName == ResourceSettings.Config.WaterPure))
            {
                var water = bodyCrustalComposition.FirstOrDefault(m => m.ResourceName == ResourceSettings.Config.WaterPure);
                var heavywaterAbundance = water.ResourceAbundance / 6420;
                bodyCrustalComposition.Add(new CrustalResource(ResourceSettings.Config.WaterHeavy, heavywaterAbundance, "HeavyWater", new[] { "HeavyWater", "D2O", "DeuteriumWater" }));
            }
        }

        private static float GetAbundance(string resourceName, int refBody)
        {
            return ResourceMap.Instance.GetAbundance(new AbundanceRequest()
            {
                ResourceType = HarvestTypes.Planetary,
                ResourceName = resourceName,
                BodyId = refBody,
                CheckForLock = false
            });
        }
    }
}
