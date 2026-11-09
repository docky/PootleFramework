"""Validate measured live Legs Animator output; run after both 45-second recordings."""
import json, sys
from pathlib import Path
folder=Path(__file__).resolve().parents[2]/'Builds/Quadruped Trials/Videos'
results=[]
for animal in (sys.argv[1:] or ['pink-horse','toy-fawn']):
 p=folder/(animal+'-report.json')
 data=json.loads(p.read_text())
 checks={
  'completed_course':data['distance']>=12.29,
  'all_legs_step':min(data['steps'])>=20,
  'no_invalid_bones':data['invalidSamples']==0,
  'no_missed_ground':data['groundMisses']==0,
  'three_supporting_legs':data['minimumSupports']>=3,
  'no_overextension':data['maxStretchRatio']<=1.001,
  'stance_contact_95pct_within_2cm':data['stanceErrorP95']<=.02,
  'stance_contact_max_within_5cm':data['stanceErrorMax']<=.05,
  'stance_slide_95pct_within_2mm_per_sample':data['stanceSlideP95']<=.002,
  'visible_swing_clearance':data['maxSwingClearance']>=.035,
  'independent_foot_heights':data['maxFootHeightDifference']>=.10,
  'sufficient_sample_count':data['sampledFrames']>=1000,
 }
 results.append({'animal':data['animal'],'passed':all(checks.values()),'checks':checks,'measurements':data})
print(json.dumps(results,indent=2))
(folder/'verification.json').write_text(json.dumps(results,indent=2)+'\n')
raise SystemExit(0 if all(r['passed'] for r in results) else 1)
