1.0.17 (October 2nd 2026)

Fixed:
- Build error due to editor-only code being unintentionally included (regression from 1.0.16)

1.0.16 (September 23rd 2026)

Added:
- Spline Curve Mesher: Option to specify the sample sampling interval distance
- Spline sampling distance interval now dynamically scales with the spline length. 75% fewer samples are now used, withouth any noticeable visual difference.

Changed:
- Rebuild Triggers: "On Start()" has been renamed to "On Load" to better describe its behaviour.
- Spline Fill Mesher: Minimum triangle size is now 0.02.

Fixed:
- Spline Mesher Standard migration tool: Error when a Spline Mesher Standard component was used on a child object in a prefab.
- Spline Curve Mesher: Mesh using the world-align axis instead of the Spline Container's rotation if the Rotation Align (X/Y/Z) options were disabled.
- World-Space UV option not creating a seamless UV between segments.
- Spline Mesher rebuilding when exiting Play mode in the editor if the "On Start()" rebuild trigger was enabled.

1.0.15 (September 9th 2026)

Added:
- Spline Curve Mesher: Option to use world-space projected UVs.

Fixed:
- Include Layer for colliders not being the correct default setting.
- Lightmapping possibly failing when Spline Fill Mesher components are present. Meshes are now converted back to 32-bit before lightmapping.

1.0.14 (July 24th 2026)

Added:
- Cube & Cylinder input mesh, option to invert the mesh (inside out).

Changed:
- Mesh construction is now skipped if the average scale of a segment is far too small. Instead mesh/colliders are cleared.
- Spline Curve Mesher: Improved blending of start/end when Splines are closed with a linear knot.

Fixed:
- Issue in Unity 6 where colliders would fail to generate under certain Spline conditions (regression since 1.0.13).
- End cap not being positioned at the actual end of the mesh if the "Even Only" distribution option was enabled.

1.0.13 (July 9th 2026)

Changed:
- Updated deprecated Unity API's in preperation for Unity 6.6+
- Lightmap UV generation now ignores components with the "Collider Only" option enabled.
- Errors will now be logged in a build if the platform doesn't support the SNorm8 of Float16 data formats.

Fixed:
- A minor gap appearing on closed splines of a considerable length.

1.0.12 (June 19th 2026)

Fixed:
- UI drawing error if a URP/HDRP project had invalid rendering layer names, causing the inspector UI to not fully render.
- Spline Curve Mesher: Normals of the final mesh not being correct if the Root transform was separate from the Spline transform and had a rotation applied.

1.0.11 (May 29th 2026)

Added:
- Cave Tunnel example to demo scene.

Fixed:
- Vertex colors being limited to half their intended value range (regression since 1.0.9).

1.0.10 (May 27th 2026)

Added:
- Spline Curver Mesher: Option to apply trimming also to the "Over Spline" scale curve.
- Spline Curver Mesher: Option to smooth linear knots under Distribution settings.
- Migration tool: 
  * Option to use prefabs from selection
  * Option to only use root objects (skipping nested prefabs).
  * Context menu option on Spline Mesher Standard components to convert a specific instance.

Changed:
- A gap no longer appears for closed splines with linear knots. Tangents are now blended between the first and last knots.
- An error is now thrown if a spline curve cannot be evaluated in the editor or development builds (NaN knot rotations or overlapping knots).
- Scale over Spline curve now better supports short curves through dynamic resolution scaling.

Fixed:
- Using the Trim End parameter causing intermediate flickering due to rounding errors.
- Error about scene not being loaded when having the Project Settings section open and unloading a scene with 1 or more Spline Mesher components.
- Adding a Spline caused scale/roll/colors/conforming data to be set up with the wrong index unit, until a component parameter was altered.

1.0.9 (May 6th 2026)

Added:
- "DestroyMeshes" public function, to destroy all meshes created by Spline Mesher components. Now also called in OnDestroy(). May be used for controlled cleanup.
- Menu items under Help/Spline Mesher Pro. To open the Help Window, Demo Scene, Preferences and Project Settings easily.

Fixed:
- Duplicate Spline Mesher instances not retaining the mesh from the original.
- Mesh not auto-rebuilding when assigning a Spline Container to a Spline Mesher component, when it had none.
- Light Probe and Rendering Layer Mask on created Mesh Renderer components not being the correct default setting.
- Normal map shading breaking on meshes in some cases when generating lightmap UVs.

Changed:
- Curve Mesher, no longer allows negative vertex color values. This is to add compatibility for external scripts needing to access the vertex colors from meshes.

1.0.8 (March 31st 2026)

Added:
- Spline Mesher class: AddCachedSpline, UpdateCachedSpline and RemoveCacheSpline functions are now public. These must be used if manual rebuilding is performed through code.

Changed:
- Mesh generation now correctly bails out if a spline is provided with a NaN length (editor and development builds only).
- A null-check is now performed on Mesh Filter components, in case they were removed externally.
- A notification is now displayed if any of the Spline-related rebuild triggers are disabled, to indicate API calls are then required to manually handle spline changes.

Fixed:
- Input mesh not being correctly shaded if a negative scale was applied.
- Script compile error in Unity 6.4+ (regression since v1.0.7)

1.0.7 (March 26th 2026)

Added:
- Project Settings: option to hide the version update notice.
- Spline Mesh Segment component. Now has its spline curve range, tile length and tile count exposed
- Spline Mesh Segment component. Shorthand functions: GetSplineContainer, GetSpline and GetNearestPointOnSpline
- Spline Fill Mesher: Noise Direction parameter (to restrict displacement to only up- or down offsets) + Falloff parameter.
- Project Settings: Option to switch to 16-bit precision for vertex positions (for lower memory usage).

Changed:
- Meshes now use a compressed format, yielding a 50% reduction in memory usage. For lightmapped spline meshes uncompressed meshes are still required.
- Fill Mesher: minimum triangle size is now 0.05. The actual minimum value used scales up to 0.5 for splines longer than 50m.
- Fill Mesher: Lightmap UV is now generated in the same way as the Render UV. As this amounts to an identical UV to the Unity-generated one.
- Pivot points of Spline Curve Mesher segments are now at approximately the center of their curve segment

Fixed:
- Stray mesh objects remaining in memory when instantiating and destroying a Spline Mesher instance.

Removed:
- Fill Mesher output section: "Store Gradients in UV". No reason to disable this option, since the data used is always present anyway.
- Fill Mesher output section: Lightmap UV generation settings, rendered obsolete.

1.0.6 (March 17th 2026)

Added:
- Collision section, field to assign a Physics Material
- Curve Mesher: vertex color width gradient "Mirrored" option. If disabled the gradient runs from left to right.
- Fill Mesher: Reverse Faces and Flip Normals options.

Changed:
- Updated Collections package dependency to v2.6.2

Fixed:
- Migration tool, rotation value not being copied over.
- Deprecation warnings regarding "GetInstanceID" usage in Unity 6.4+
- Collider meshes of a duplicated Spline Mesher appearing to be linked to the original.

1.0.5 (March 13th 2026)

Changed:
- Curve Mesher: if a spline is used that's shorter than the input mesh, the mesh is scaled down to fit the spline. Previous behaviour was to skip mesh generation.

Fixed:
- Memory leak when using the Spline Curve Mesher component (regression since 1.0.4)
- Warning being spammed when trying to visualize the output segments and having the "Collider Only" option enabled.

1.0.4 (March 10th 2026)

Added:
- Spline Mesh Segment UI: Preview window for the created mesh.
- Collision: Include/Exclude layer masks

Fixed:
- Changing the "Keep Readable" option had no effect on already created meshes.
- Curve Mesher: Warning in the inspector when using an input mesh that is not readable while in playmode.

Changed:
- Improved handling of runtime mesh generation when input mesh is not readable.
- Minor performance improvement (~5%) by assuming the Root transform is not non-uniformly scaled/skewed (a warning is now displayed if it is).

1.0.3 (March 9th 2026)

Added:
- Segmenting Mode. Determines how the mesh is split into segments. (By Length, Every Tile, Specific and None).
- Cylinder input mesh: Caps creation even when the "Hollow" option is disabled.
- Vertex Color width gradient: Option to factor in the scale of the mesh, so the offset remains consistent.

Fixed:
- "SetSubMesh" warnings when altering the spline of a mesh with more than 1 material.
- Collider not scaling smaller than 1m (XY), if the "Collider Only" option was enabled.
- Scale Tool: Z-value of grid snapping settings not being respected.
- Extra segments created whilst editing a prefab not being removed when performing an Undo operation.

Changed:
- Spline data (Scale/Roll/Colors/Conforming) for removed splines is now cleared immediately.

1.0.2 (March 4th 2026)

Added:
- Support for usage with prefabs.
- Scale Tool: support for natively grid snapping scale values.

Changed:
- Splines shorter than 1m long are now allowed.
- SplineMeshContainer and SplineMeshSegment components now have an inspector UI showing relevant stats.
- The warning regarding prefabs and procedural meshes can now be dismissed.
- SplineMesher base component is now prevented from being manually added to a GameObject.

Fixed:
- Using the same Root transform for a Curve Mesher and Fill Mesher resulted in both trying to adopt eachother's output meshes.
- Collider some times not appearing to be generated, if the "Custom" was first selected with no mesh assigned.
- Input mesh alignment not correct when also applying a rotation.
- Script error when Bakery was installed (missing assembly reference).
- Leak error messages when calling Rebuild() from a script whilst adding new Splines.
- Error thrown when deleting the last point in the Scale editor.

1.0.1 (March 2nd 2026)

Fixed:
- Normals of Cube input mesh being flipped on the left face. Resulting in a gap in colliders.
- Several meshes in the demo scene inadvertently using materials from the Standard package.

1.0.0 (March 1st 2026)
This is a major rewrite, re-release and a new asset altogether. A migration tool is included to convert Spline Mesher Standard instances.

All functionality has been recreated from the ground up around a multithreaded design using Jobs, Burst and the low-level MeshData API. Performance has increased by at least 50x.

Added:
- Spline Fill Mesher component. Generates a mesh from a Spline's contour with a uniform topology
* Procedural displacement through bulging and/or noise
* Granular UV controls: Fitting, tiling, offset, rotation and flipping
* Conforming to underlying colliders
* Collision mesh generation
* Distance field baked into UV for shader effects (eg. gradients, water shorelines)

- New demo content
* Wind tube
* Aurora borealis
* Street curbs
* Stylized lake
* Rope bridge
* River
* Cliff faces
* Party lights
  * Tentacle
  * Conveyor belt
  * Tire tracks

- Inspector enhancements:
* Buttons to toggle Wireframe and UV visualization
* Ability to drag & drop a material
* Triangle/vertex count display + memory size

- Collider generation:
* Option to use an adjustable Cube/Cylinder/Plane mesh.
* Colliders can now have RigidBodies auto-attached and a separate layer
* Collision/Trigger events across the spline (C# or through the inspector)

- Vertex colors:
* A base color can now be specified (overwriting the input mesh's colors)
* A gradient can now be auto-generated along the start/end of the mesh (configurable length and falloff) and across the width.

- Other
- Input mesh can now be a configurable Cube, Tube or Plane mesh.
- Output mesh can now be split into segments of a maximum length.
- Support for MeshLOD in Unity 6.2. LODs can now be auto-generated when the scene saves (or manually through script)
- Conforming start/end falloff controls.
- Caps can now be aligned on specific axis.

Changed:
- Script namespace has changed to "sc.splinemesher.pro.runtime"
- Spline Mesher component is now called "Spline Curve Mesher"
- A separate mesh/collider objects is now created per spline.
- Render- and Collision mesh are now created in parallel for far better performance.
- Lightmap UV generation behaviour can now be configured in a “Project Settings” section, so can be commited to source control

Removed:
- Conforming, "Terrains Only" option. No longer possible with Job system raycasting.