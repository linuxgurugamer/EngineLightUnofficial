/*
	Copyright (c) 2016

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

/*
 *          [KSP] Engine Light Mod 
 *          Made by Tatjam (Tajampi)  
 *                TajamSoft
 *         ---------------------------
 *               Contributors: 
 *                Saabstory88
 *            SpaceTiger (aka Xarun)
 *				ToXik-yogHurt (code refactoring asshole)
 *			
 *--------------------------------------------------
 *
 * Notes:
 *      
 *  I'm implementing my own light, to make sure I don't break anything
 * 
 * 
 * Todo:
 * 
 * Give nuclear engines some temperature-dependant light emission?
 * Runtime modification of light colours & locations:
 *		to make configs for new engines easier to make
 * 
 */

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;


namespace EngineLightRelit
{
	[Flags]
	public enum LightStates{Disabled=0, Exhaust=1, Emissive=2, Both=3};

	// a little single-purpose circular buffer to perform a rolling average on random values
	internal class JitterBuffer
	{
		const int BUFFERSIZE = 5;
		protected float[] buffer = new float[BUFFERSIZE];
		protected int pointer = 0;

		public JitterBuffer()
		{
			this.reset();
		}

		public void reset()
		{
			for (int i=0; i<BUFFERSIZE; ++i)
				buffer[pointer] = 0;
		}

		public void nextJitter()
		{
			++pointer;
			pointer %= BUFFERSIZE;
			buffer[pointer] = UnityEngine.Random.value; // ideally you should have to provide a random number source in the constructor
		}

		public float getAverage(bool autoJitter=true)
		{
			if (autoJitter)
				this.nextJitter();

			float average = 0;
			for (int i=0; i<BUFFERSIZE; ++i)
				average += buffer[i];
			return average / BUFFERSIZE;
		}
	}

	// cater for rapier / multimode engines
	internal class EngineModule
	{
		public bool hasEmissive { get; protected set;}
		public Transform transform { get {return modules[0].transform;} } // hope this works...
		public bool isEnabled { get {return modules[0].isEnabled;} }

		protected List<ModuleEngines> modules;
		protected int moduleCount; // since it won't change - don't waste cycles on List.Count()
		
		protected FXModuleAnimateThrottle engineEmissiveModule;

		public EngineModule(Part part)
		{
			// RAPIERs have two engine modules - they share an emissive texture, but not a throttle value
			modules = part.FindModulesImplementing<ModuleEngines>(); // do it once, do it in the right place
			moduleCount = modules.Count;
			if (moduleCount < 1)
				throw new Exception("could not locate an engine on part: " + part.name);
			foreach (var module in modules)
			{
				if (module == null) // this is how much I trust the KSP API...
					throw new Exception("could not really locate an engine on part: " + part.name);
			}

			hasEmissive = false;
			try
			{
				// should pretty reliably test that we can read from the emissive
				engineEmissiveModule = part.FindModuleImplementing<FXModuleAnimateThrottle>();
				float emissive = 1 + engineEmissiveModule.animState; // should throw nullref on no emissive
				if (emissive >= 1)
					hasEmissive = true;
			}
			catch (Exception)
			{
				/* nothing to do - no emissive isn't a big deal really */
			}
		}

		public float getThrottle()
		{
            float throttle = 0;
            if (moduleCount == 1)
            {
                if (modules[0].EngineIgnited)
                {
                    throttle = modules[0].currentThrottle;
                }
            }
            else
            {
                // return the greatest throttle setting of available engine modules
                for (int i = 0; i < moduleCount; ++i)
                {
                    if (modules[i].EngineIgnited)
                    {
                        throttle = (modules[i].currentThrottle > throttle) ? modules[i].currentThrottle : throttle;
                    }
                }

            }
            return throttle;
        }

		public float getEmissive()
		{
			if (hasEmissive)
				return engineEmissiveModule.animState;
			else
				return 0f;
		}

		public float getMaxThrust(bool fromEnabledModulesOnly=false)
		{
			/* this approach means multimode engines with very disparate thrust ratings will have
			 * incorrect light intensity when they switch to low-thrust mode, possibly on startup
			 * acceptible for now...
			 * set the optional parameter when doing recalculation for mode-switching */
			if (moduleCount == 1)
				return modules[0].maxThrust;
			else
			{
				// return the greatest thrust setting of (operational) engine modules
				float thrust = 0;
				for (int i=0; i<moduleCount; ++i)
				{
					if (fromEnabledModulesOnly && !modules[i].isEnabled)
						continue; // skip disabled modules
					thrust = (modules[i].maxThrust > thrust) ? modules[i].maxThrust : thrust;
				}

				return thrust;
			}
		}

		public Vector3 getFirstThrustTransform()
		{
			return modules[0].thrustTransforms[0].position;
		}

		public Vector3 getAverageThrustTransform(float zOffset=1.3f)
		{
			// average the available thrust transforms, like so
			Vector3 averageThrustTransform = new Vector3(0,0,0);
			int transformCount = 0;
			foreach (var module in modules)
			{
				transformCount += module.thrustTransforms.Count; 
				foreach (var transform in module.thrustTransforms)
					averageThrustTransform += transform.position; // mmm, vector addition, tastey
			}

			// engines with no thrust transforms will throw a div-by-zero, but engines with no thrust transforms are probably broken anyway
			// this could be wrapped in 'if (transformCount > 1)' but the branch test would probably be slower than just doing the divisions
			averageThrustTransform /= transformCount; 
			averageThrustTransform.y *= zOffset; // push the light source slightly away from the engine body

			return averageThrustTransform;
		}
	}

	public class EngineLightEffect : PartModule
	{
		public const float LIGHT_CURVE = -0.0000004f;
		public const float LIGHT_LINEAR = 0.0068f;
		public const float LIGHT_MINIMUM = 0.1304f;
		public const float MAXIMUM_LIGHT_INTENSITY = 40;
		public const float MAXIMUM_LIGHT_RANGE = 12;
		public const float MAXIMUM_JITTER = 12;
		public const float MINIMUM_LIGHT_INTENSITY = 0.5f;
		public const float MINIMUM_LIGHT_RANGE = 8;	

		// Variables:

			/* CAUTION - don't modify these fields at runtime and expect much to happen */
		// on start many of these are used to initialise internal Color structs and then never read again

		[KSPField]
		public float _lightPower = 1.0f; //LightSource power (gets a percentage based on thrust)

		float lightPowerAdjust = 0f;
		// Utils.clampValue(_lightPower, 0, MAXIMUM_LIGHT_INTENSITY, "light intensity");
		public float LightPower {  get { return Utils.clampValue(_lightPower * lightPowerAdjust * HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightPower, 0, MAXIMUM_LIGHT_INTENSITY, "light intensity"); } }

		[KSPField]
		public float _lightRange = 9f; // modified, down from 40 to 9

		public float LightRange {  get { return _lightRange * HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightRange; } }

		[KSPField]
		public float exhaustRed = 1.0f;

		[KSPField]
		public float exhaustGreen = 0.88f;

		[KSPField]
		public float exhaustBlue = 0.68f;

		[KSPField]
		public float jitterMultiplier = 0.1f; // modified, down from 10 to 0.1

		[KSPField]
		public float multiplierOnIva = 0.5f;

		// manual offset of light location
		[KSPField]
		public float lightOffsetX = 0;

		[KSPField]
		public float lightOffsetY = 0;

		// allow manual tweaking of the exhaust location
		[KSPField]
		public float exhaustOffsetZ = 0;

		// allow manual tweaking of the emissive location
		[KSPField]
		public float emissiveOffsetZ = 0;
		
#region colour curves for emissive light
		// linear
		[KSPField]
		public float emissiveRed = 0.6f;

		[KSPField]
		public float emissiveGreen = 0.0f;

		[KSPField]
		public float emissiveBlue = 0.0f;

		// logarithmic
		[KSPField]
		public float emissiveLogRed = 0.4f;

		[KSPField]
		public float emissiveLogGreen = 0.1f;

		[KSPField]
		public float emissiveLogBlue = 0f;

		// quadratic
		[KSPField]
		public float emissiveQuadRed = 0f;

		[KSPField]
		public float emissiveQuadGreen = 0.5f;

		[KSPField]
		public float emissiveQuadBlue = 0.05f;
#endregion


		[KSPField]
		public float engineEmissiveMultiplier = 1.33f; // linear scale factor for emissive brightness

		[KSPField]
		public bool enableEmissiveLight = true;

		[KSPField]
		public float _lightFadeCoefficient = 0.8f; // keep less than 1	

		public float LightFadeCoefficient {  get { return _lightFadeCoefficient * HighLogic.CurrentGame.Parameters.CustomParams<EL>().lightFadeCoefficient; } }

		protected bool initOccurred = false; // this is a bit icky - but it's just for debugging

		protected GameObject engineLightObject; // this allows for repositioning of the light at run-time, if that turns out to be needed
		protected Light engineLight;

		protected LightStates lightState = LightStates.Disabled;
		protected LightStates lastFrameLightState = LightStates.Disabled;

		EngineModule engineModule = null;

		JitterBuffer jitterBuffer;

		// used to alter & scale the base emissive colour as the engine is running
		protected Color emissiveColorBase; // constant offset
		protected Color emissiveColorLogModifier; // logarithmically with emissive intensity (more power early)
		protected Color emissiveColorQuadModifier; // quadratically with emissive intensity (more power later)
		protected Color emissiveColor; 
		protected Color exhaustColor;

		protected Vector3 averageThrustTransform;
		protected Vector3 lightOffset;

		protected bool ivaEnabled = false;
		protected double lastReportTime = 0;
		protected float throttle = 0;
		protected float jitteredThrottle = 0;
		protected float lastFrameThrottle = 0;
		protected float intensity = 0;
		protected float emissiveValue = 0;
		protected float emissiveIntensity = 0;
		protected float minimumLightRange = MINIMUM_LIGHT_RANGE;		

		public void initEngineLights()
		{
			try 
			{
				// wrap the parts engine module(s) and FX modules for simpler calls later	        
				if (engineModule == null) engineModule = new EngineModule(this.part);
				
				if (!engineModule.hasEmissive) // not all engines have emissives
					enableEmissiveLight = false;
				else
				{
					emissiveColor = emissiveColorBase = new Color(emissiveRed, emissiveGreen, emissiveBlue);
					emissiveColorLogModifier = new Color(emissiveLogRed, emissiveLogGreen, emissiveLogBlue);
					emissiveColorQuadModifier = new Color(emissiveQuadRed, emissiveQuadGreen, emissiveQuadBlue);
				}

				exhaustColor = new Color(exhaustRed, exhaustGreen, exhaustBlue);

				// increase the minimum light range for larger parts
				AttachNode node = this.part.FindModuleImplementing<AttachNode>();
				if (node != null)
					minimumLightRange += node.radius;

				jitterBuffer = new JitterBuffer();
				
				// cache function call
				float maxThrust = engineModule.getMaxThrust();

				// calculate light power from engine max thrust - follows a quadratic:
				lightPowerAdjust = ((LIGHT_CURVE * maxThrust * maxThrust)
							+ (LIGHT_LINEAR * maxThrust)
							+ LIGHT_MINIMUM);
							//* LightPower; // use the multiplier read from config file

				//_lightPower = Utils.clampValue(_lightPower, 0, MAXIMUM_LIGHT_INTENSITY, "light intensity");

				// old config used 40 as default - but that was way too high:
				// it caused light to reach planetary surfaces from low orbit: 20 is a more sensible max value
				// less considering the minimum offset I've introduced
				_lightRange = Utils.clampValue(_lightRange, 0, MAXIMUM_LIGHT_RANGE, "light range");

				jitterMultiplier = Utils.clampValue(jitterMultiplier, 0, MAXIMUM_JITTER, "jitter multiplier");

				//Make lights: (Using part position)

				averageThrustTransform = engineModule.getAverageThrustTransform();

				GameObject engineLightObject = new GameObject();
				engineLightObject.AddComponent<Light>();
				Light engineLight = engineLightObject.GetComponent<Light>();

				// Light Settings:
				engineLight.type = LightType.Point;
				engineLight.color = exhaustColor;
				engineLight.enabled = false;

				// Transform Settings:
				engineLightObject.transform.parent = engineModule.transform;
				engineLightObject.transform.forward = engineModule.transform.forward; //not really required?
				engineLightObject.transform.position = averageThrustTransform; 

				// this (like colour) is applied per-frame - because it looks better
				lightOffset = new Vector3(lightOffsetX, 0, lightOffsetY);

				// and we're done - register our local variables and flag success
				this.engineLightObject = engineLightObject;
				this.engineLight = engineLight;
				initOccurred = true;

				// this is how you do debug only printing...	
#if DEBUG
	Utils.log("Light calculations (" + this.part.name + ") resulted in: " + LightPower);
	Utils.log("coords of engine: " + engineModule.transform.position);
	Utils.log("coords of thrust: " + averageThrustTransform);
	//Utils.log("coords of thrust offset: " + thrustOffset);
	Utils.log("coords of light: " + engineLightObject.transform.position);
#else
	Utils.log("Detected and activating for engine: (" + this.part.name + ")");
#endif
			}
			catch (Exception exception)
			{
				Utils.log("Error in init(): " + exception.Message);
			}
		}


        public void Start()
		{
            if (!HighLogic.LoadedSceneIsFlight || initOccurred)
				return; //Beware the bugs!
	
#if DEBUG
	Utils.log("Initialized part (" + this.part.partName + ") Proceeding to patch!");
#endif

			initEngineLights(); // allows manual init / re-init of module, probably
		
		}

		public void OnDestroy()
		{
			// I don't much trust Unity not to leak memory otherwise y'see
			Destroy(this.engineLight);
			Destroy(this.engineLightObject);
		}

		public void FixedUpdate()
		{
            if (!HighLogic.LoadedSceneIsFlight)
                return;
	        if (!initOccurred)
	        {
#if DEBUG
                Utils.log("FixedUpdate() called before Start() - wtf even?");
#endif
		        return; // I guess this might happen for a frame or two while a scene is still loading? should be harmless - we can wait
	        }
			if (  DynamicSettings.ConfigChanged)
            {
				initEngineLights();
				DynamicSettings.ConfigChanged = false;
			}
			try
			{
				// these _really_ shouldn't be happening - if one does we need to fix it, not ignore it. 
				// the performance hit from throwing exceptions should encourage that.
				if (this.engineLightObject == null)
					throw new Exception("Light Transform Object failed to initialise correctly");
				if (this.engineLight == null)
					throw new Exception("Light failed to initialise correctly");
				if (this.engineModule == null)
					throw new Exception("EngineModule failed to initialise correctly");

				ivaEnabled = Utils.isIVA();
				if (MapView.MapIsEnabled || (ivaEnabled && multiplierOnIva < 0.1f) || engineLight.intensity < 0.1)
					engineLight.enabled = false;
				else
					engineLight.enabled = true;

				throttle = engineModule.getThrottle(); // cache this - don't trust KSPs properties

				lightOffset.y = 0; // reset the light offset

				// smooth the drop-off in intensity from sudden throttle decreases
				// the exhaust is still somewhat present, and that's what's supposed to be emitting the light
				if (lastFrameThrottle > 0 && (lastFrameThrottle - throttle / lastFrameThrottle) > (1 - LightFadeCoefficient))
					throttle = lastFrameThrottle * LightFadeCoefficient;

				// d'awww, it's a wee finite state machine!
				lightState = LightStates.Disabled;
				if (throttle > 0)
					lightState = lightState | LightStates.Exhaust;
				if (enableEmissiveLight)
				{
						emissiveValue = engineModule.getEmissive(); // cache this
					if (emissiveValue > 0.1f) // don't glow when the emissive is too dull
						lightState = lightState | LightStates.Emissive;
				}

				switch (lightState)
				{
					case LightStates.Exhaust:
						engineLight.color = exhaustColor; // when restarting an engine
						setIntensityFromExhaust();
						lightOffset.y -= exhaustOffsetZ;

						if (ivaEnabled)
							engineLight.intensity *= multiplierOnIva;

						break;


					case LightStates.Emissive:
						// emissive-only light needs these reset
						engineLight.intensity = 0;
						engineLight.range = minimumLightRange;
						lightOffset.y -= emissiveOffsetZ;

						if (!ivaEnabled) // can't see emissive from inside
							setIntensityFromEmissive();

						break;


					case LightStates.Both:
						setIntensityFromExhaust();
						setIntensityFromEmissive();
						lightOffset.y -= emissiveOffsetZ;
						// move the light towards the exhaust plume (proportionally) as exhaust light dominates
						lightOffset.y -= (((engineLight.intensity - emissiveIntensity) / engineLight.intensity) * (exhaustOffsetZ - emissiveOffsetZ));

						if (ivaEnabled)
							engineLight.intensity *= multiplierOnIva;

						break;


					case LightStates.Disabled:
						engineLight.enabled = false;

						break;
				}
				
				engineLightObject.transform.localPosition = lightOffset;
				
				engineLight.cullingMask &= ~(1 << vessel.mainBody.scaledBody.layer); //Prevents light from reaching the planet while in space

#if DEBUG

	if (lastReportTime < Time.time)
	{
		if (engineModule.isEnabled)
		{
			Utils.log("part: " + part.name);
			Utils.log("fade rate: " + LightFadeCoefficient);
			Utils.log("lightstate: " + lightState);
			Utils.log("throttle: " + throttle);
			Utils.log("jittered throttle: " + jitteredThrottle);
			Utils.log("previous throttle: " + lastFrameThrottle);
			Utils.log("intensity: " + engineLight.intensity);
			Utils.log("color: " + engineLight.color);
			Utils.log("range: " + engineLight.range);
			if (enableEmissiveLight)
			{
				Utils.log("emissive: " + emissiveValue);
				Utils.log("emissive intensity: " + emissiveIntensity);
			}
			Utils.log("coords of engine: " + engineModule.transform.position);
			Utils.log("coords of thrust: " + averageThrustTransform);
			//Utils.log("coords of thrust offset: " + thrustOffset);
			Utils.log("coords of light: " + engineLightObject.transform.position);
			Utils.log("");
		}

		lastReportTime = Time.time + 1;
	}
		
#endif

				lastFrameThrottle = throttle;
			}		
			catch (Exception exception)
			{
				Utils.log("Error in FixedUpdate(): " + exception.Message);
			}
		}


		// the part of me that values readability wants these functions out here, the part of me concerned
		// with performance wants to find a way to inline them - neatness wins for today
#region main light intensity calculation methods
		protected void setIntensityFromExhaust()
		{
			//this is how we keep the maths to a minimum
			jitteredThrottle = throttle + jitterBuffer.getAverage() * jitterMultiplier; // per-frame jitter was annoying, now it's smoothed
			engineLight.intensity = MINIMUM_LIGHT_INTENSITY + LightPower * jitteredThrottle * jitteredThrottle; // exponential increase in intensity with throttle
			engineLight.range = minimumLightRange + LightRange * jitteredThrottle; // linear increase in range with throttle
		}

		protected void setIntensityFromEmissive()
		{
			// harder maths than the main engine light (ironically?)

			// Log10 is nice mathematically, we stay in pretty much the same range of values
			// but get a boost in intensity at low emisssitivity values
			emissiveIntensity = (float)(1 + Math.Log10(emissiveValue));
						
			// shift the color from red towards yellow (white?) as the emissive increases
			// summing a logarithm and a square over the range 0.1 -> 1 gives a roughly linear output
			// splitting the emissive into two modifiers lets you fade the colour channels up slightly-independantly
			// it's not as flexible as 3 separate-channel polynomial curves, but it's also much faster to run real-time
			emissiveColor = emissiveColorBase;
			emissiveColor += emissiveValue * emissiveValue * emissiveColorQuadModifier;
			emissiveColor += emissiveIntensity * emissiveColorLogModifier;
			
			// spread the color out between exhaust light and emissive light
			// just using an average causes emissive to overpower exhaust light, weight the colour balance towards the active light
			emissiveIntensity *= engineEmissiveMultiplier;
			engineLight.color = (exhaustColor * engineLight.intensity * 3) + (emissiveColor * emissiveIntensity);
			engineLight.color /= ((engineLight.intensity * 3) + emissiveIntensity); 
			engineLight.intensity += emissiveIntensity;
		}
#endregion

		// don't do this: use pragmas to log informational messages when developing - don't hide serious error messages, even in production
		// few things suck more than a live bug with an empty error log!
		//Useful, checks for debug before printing, do not use if message is important
		//public void print(object text)
		//{
		//	if (isDebug)
		//	{
		//		Debug.Log(text);
		//	}
		//}
	}
}