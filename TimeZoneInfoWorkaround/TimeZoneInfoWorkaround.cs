using System;
using System.Reflection;
using System.Globalization;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

//Setup the GetTimeZoneData icall in the proper namespace and class to match the runtime
namespace System
{
    public class CurrentSystemTimeZone
    {
	// Internal method to get timezone data.
	//    data[0]:  start of daylight saving time (in DateTime ticks).
	//    data[1]:  end of daylight saving time (in DateTime ticks).
	//    data[2]:  utcoffset (in TimeSpan ticks).
	//    data[3]:  additional offset when daylight saving (in TimeSpan ticks).
	//    name[0]:  name of this timezone when not daylight saving.
	//    name[1]:  name of this timezone when daylight saving.
	[MethodImplAttribute(MethodImplOptions.InternalCall)]
	public static extern bool GetTimeZoneData (int year, out Int64[] data, out string[] names);
    }
}

namespace Unity
{
    public class CustomTimeZoneExample
    {
	private enum TimeZoneData
	{
	    DaylightSavingStartIdx,
	    DaylightSavingEndIdx,
	    UtcOffsetIdx,
	    AdditionalDaylightOffsetIdx
	};
	
	private enum TimeZoneNames
	{
	    StandardNameIdx,
	    DaylightNameIdx
	};
	
	public static void Main(String[] args)
	{
	    Int64[] data;
	    string[] names;
	    if (!System.CurrentSystemTimeZone.GetTimeZoneData (DateTime.Now.Year, out data, out names))
		throw new NotSupportedException ("Can't get timezone name.");
	    
	    TimeSpan utcOffsetTS = TimeSpan.FromTicks(data[(int)TimeZoneData.UtcOffsetIdx]);
	    char utcOffsetSign = (utcOffsetTS >= TimeSpan.Zero) ? '+' : '-';
	    string displayName = "(GMT" + utcOffsetSign + utcOffsetTS.ToString(@"hh\:mm") + ") Local Time";
	    string standardDisplayName = names[(int)TimeZoneNames.StandardNameIdx];
	    string daylightDisplayName = names[(int)TimeZoneNames.DaylightNameIdx];
	    
	    //Create The Adjustment Rules For This TimeZoneInfo.
	    var adjustmentList = new List<TimeZoneInfo.AdjustmentRule>();
	    for(int year = 1970; year <= 2037; year++)
	    {	    
		if (!System.CurrentSystemTimeZone.GetTimeZoneData (year, out data, out names))
		    throw new NotSupportedException ("Can't get timezone name.");
		
		DaylightTime dlt = new DaylightTime (new DateTime (data[(int)TimeZoneData.DaylightSavingStartIdx]),
						     new DateTime (data[(int)TimeZoneData.DaylightSavingEndIdx]),
						     new TimeSpan (data[(int)TimeZoneData.AdditionalDaylightOffsetIdx]));
		
		DateTime dltStartTime = new DateTime(1, 1, 1).Add(dlt.Start.TimeOfDay);
		DateTime dltEndTime = new DateTime(1, 1, 1).Add(dlt.End.TimeOfDay);
		
		TimeZoneInfo.TransitionTime startTime = TimeZoneInfo.TransitionTime.CreateFixedDateRule(dltStartTime, dlt.Start.Month, dlt.Start.Day);
		TimeZoneInfo.TransitionTime endTime = TimeZoneInfo.TransitionTime.CreateFixedDateRule(dltEndTime, dlt.End.Month, dlt.End.Day);
		
		//mktime only supports dates starting in 1970, so create an adjustment rule for years before 1970 following 1970s rules 
		if (year == 1970)
		{
		    TimeZoneInfo.AdjustmentRule firstRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(DateTime.MinValue,
													     new DateTime(1969, 12, 31),
													     dlt.Delta,
													     startTime,
													     endTime);
		    adjustmentList.Add(firstRule);
		}
		
		TimeZoneInfo.AdjustmentRule rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(year, 1, 1),
												    new DateTime(year, 12, 31),
												    dlt.Delta,
												    startTime,
												    endTime);
		adjustmentList.Add(rule);
		
		//mktime only supports dates up to 2037, so create an adjustment rule for years after 2037 following 2037s rules 
		if (year == 2037)
		{
		    TimeZoneInfo.AdjustmentRule lastRule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(new DateTime(2038, 1, 1),
													    DateTime.MaxValue,
													    dlt.Delta,
													    startTime,
													    endTime);
		    adjustmentList.Add(lastRule);
		}
	    }
	    
	    TimeZoneInfo local = TimeZoneInfo.CreateCustomTimeZone("local",
								   utcOffsetTS,
								   displayName,
								   standardDisplayName,
								   daylightDisplayName,
								   adjustmentList.ToArray(),
								   false);
	    
	    //Set the C# library's local time field via reflection
	    FieldInfo cachedStaticData = typeof(TimeZoneInfo).GetField("s_cachedData", BindingFlags.Static | BindingFlags.NonPublic);

	    Object cachedData = cachedStaticData.GetValue(null);
	    FieldInfo localTzField = cachedData.GetType().GetField("m_localTimeZone", BindingFlags.Instance | BindingFlags.NonPublic);

	    Console.WriteLine("UTC: " + DateTime.UtcNow);
	    Console.WriteLine("LOCAL: " + DateTime.Now);
	    //Set the local time to UTC...simulates a device that does not currently support TimeZoneInfo
	    localTzField.SetValue(cachedData, TimeZoneInfo.Utc);
	    Console.WriteLine("UTC To Local Time Fails: " + DateTime.UtcNow.ToLocalTime());
	    //Set the local time to our created one
	    localTzField.SetValue(cachedData, local);
	    Console.WriteLine("UTC To Local Time Success: " + DateTime.UtcNow.ToLocalTime());

	    //Created Custom Timezone can also be used for conversion without reflection
	    Console.WriteLine("UTC To Local Time Success No Reflection: " + TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, local));
	}
    }
}
