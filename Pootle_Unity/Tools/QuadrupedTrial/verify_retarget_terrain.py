"""Validate the imported walk's terrain pass; its authored gait may have two supporting feet."""
import json
import sys
from pathlib import Path

folder = Path(__file__).resolve().parents[2] / 'Builds/Quadruped Trials/Videos'
source = Path(sys.argv[1]) if len(sys.argv) > 1 else folder / 'pink-pony-retarget-report.json'
data = json.loads(source.read_text())
checks = {
    'completed_course': data['distance'] >= 12.29,
    'all_legs_step': min(data['steps']) >= 8,
    'no_invalid_bones': data['invalidSamples'] == 0,
    'no_missed_ground': data['groundMisses'] == 0,
    'at_least_two_physical_contacts': data.get('minimumPhysicalSupports', data['minimumSupports']) >= 2,
    'no_chain_overextension': data['maxStretchRatio'] <= 1.001,
    'stance_contact_p95_within_2cm': 0 <= data['stanceErrorP95'] <= .02,
    'stance_contact_max_within_5cm': 0 <= data['stanceErrorMax'] <= .05,
    'stance_slide_p95_within_5mm_per_sample': 0 <= data['stanceSlideP95'] <= .005,
    'visible_swing_clearance': data['maxSwingClearance'] >= .03,
    'independent_foot_heights': data['maxFootHeightDifference'] >= .1,
    'sufficient_sample_count': data['sampledFrames'] >= 1000,
}
if 'maxTerminalBend' in data:
    checks['terminal_bend_within_55_degrees'] = data['maxTerminalBend'] <= 55.01
report = {'passed': all(checks.values()), 'checks': checks, 'measurements': data}
(source.with_name(source.stem + '-verification.json') if len(sys.argv) > 1 else folder / 'retarget-terrain-verification.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
raise SystemExit(0 if report['passed'] else 1)
