"""Building kit for the 3D settlement view.

Builds every module of the kit (walls, posts, plinths, gables, roofs, chimneys, construction and decay pieces), exports each module
to its own FBX for Unity and renders a preview: sample houses in every stage seen from the settlement camera, and a sheet of all modules.
Sizes, pivots and names are the contract with the in-engine generator, described in README.md next to this file.

Run without UI:
    blender.exe -b --factory-startup --python kit_build.py -- --fbx <dir> --preview <png> [--sheet <png>]
"""
import argparse
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

CELL = 1.0
FLOOR_H = 2.6
PLINTH_H = 0.35
WALL_T = 0.2
TIMBER = 0.14
PITCH = math.radians(50)
EAVE = 0.35
GABLE_OVH = 0.3
ROOF_T = 0.12
DEPTHS = (3, 4, 5, 6)
INFILL_INSET = 0.03

MATERIALS = {
    'GM_Timber': (0.16, 0.11, 0.07),
    'GM_Daub': (0.62, 0.58, 0.50),
    'GM_Stone': (0.36, 0.35, 0.33),
    'GM_Roof': (0.20, 0.20, 0.21),
    'GM_Plank': (0.30, 0.25, 0.19),
    'GM_Window': (0.05, 0.05, 0.06),
    'GM_Rope': (0.45, 0.38, 0.25),
    'GM_Wattle': (0.22, 0.17, 0.11),
    'GM_Ground': (0.27, 0.24, 0.18),
}


def rise(depth):
    """Height of the ridge above the wall top for a house of this depth."""
    return depth / 2 * math.tan(PITCH)


class Builder:
    """Collects boxes, beams and prisms into one mesh with named material slots."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def box(self, lo, hi, mat, m=None):
        (x0, y0, z0), (x1, y1, z1) = lo, hi
        corners = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        vs = [self.bm.verts.new(m @ Vector(c) if m else Vector(c)) for c in corners]
        idx = self.mat(mat)
        for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            self.bm.faces.new([vs[i] for i in f]).material_index = idx

    def beam(self, a, b, size, mat):
        """Square-section beam from point a to point b."""
        a, b = Vector(a), Vector(b)
        d = b - a
        m = Matrix.Translation(a) @ d.to_track_quat('Z', 'Y').to_matrix().to_4x4()
        h = size / 2
        self.box((-h, -h, 0), (h, h, d.length), mat, m)

    def prism_xz(self, pts, y0, y1, mat):
        """Polygon given in the XZ plane, extruded along Y from y0 to y1."""
        front = [self.bm.verts.new((x, y0, z)) for x, z in pts]
        back = [self.bm.verts.new((x, y1, z)) for x, z in pts]
        idx = self.mat(mat)
        self.bm.faces.new(front).material_index = idx
        self.bm.faces.new(list(reversed(back))).material_index = idx
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new([front[i], back[i], back[j], front[j]]).material_index = idx

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        uv = self.bm.loops.layers.uv.new('UVMap')
        for face in self.bm.faces:
            n = face.normal
            axis = max(range(3), key=lambda k: abs(n[k]))
            for loop in face.loops:
                c = loop.vert.co
                loop[uv].uv = (c.y, c.z) if axis == 0 else (c.x, c.z) if axis == 1 else (c.x, c.y)
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        for m in self.mats:
            mesh.materials.append(material(m))
        return mesh


def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value = (*(c ** 2.2 for c in MATERIALS[name]), 1)  # palette is in sRGB, Blender wants linear
        bsdf.inputs['Roughness'].default_value = 0.85
    return mat


# ---------- facade modules: X along the wall, outer face at Y = 0 facing -Y, pivot at the bottom middle ----------

def frame(b, mid_rail=False, brace=False):
    b.box((-0.5, 0, 0), (0.5, TIMBER, TIMBER), 'GM_Timber')
    b.box((-0.5, 0, FLOOR_H - TIMBER), (0.5, TIMBER, FLOOR_H), 'GM_Timber')
    if mid_rail:
        b.box((-0.5, 0, 1.2), (0.5, TIMBER, 1.2 + TIMBER), 'GM_Timber')
    if brace:
        b.beam((-0.42, TIMBER / 2, TIMBER), (0.42, TIMBER / 2, FLOOR_H - TIMBER), TIMBER * 0.85, 'GM_Timber')


def infill(b, x0, x1, z0, z1, mat='GM_Daub'):
    if x1 > x0 and z1 > z0:
        b.box((x0, INFILL_INSET, z0), (x1, WALL_T, z1), mat)


def wall_plain(brace):
    b = Builder()
    frame(b, mid_rail=not brace, brace=brace)
    infill(b, -0.5, 0.5, TIMBER, FLOOR_H - TIMBER)
    return b


def window_cut(b):
    """Infill around a window opening and the timber frame of the opening."""
    x, z0, z1 = 0.28, 0.95, 1.65
    infill(b, -0.5, -x, TIMBER, FLOOR_H - TIMBER)
    infill(b, x, 0.5, TIMBER, FLOOR_H - TIMBER)
    infill(b, -x, x, TIMBER, z0)
    infill(b, -x, x, z1, FLOOR_H - TIMBER)
    t = 0.08
    b.box((-x - t, 0, z0 - t), (-x, TIMBER * 0.8, z1 + t), 'GM_Timber')
    b.box((x, 0, z0 - t), (x + t, TIMBER * 0.8, z1 + t), 'GM_Timber')
    b.box((-x, 0, z0 - t), (x, TIMBER * 0.8, z0), 'GM_Timber')
    b.box((-x, 0, z1), (x, TIMBER * 0.8, z1 + t), 'GM_Timber')
    return x, z0, z1


def wall_window(boarded):
    b = Builder()
    frame(b)
    x, z0, z1 = window_cut(b)
    if boarded:
        b.box((-x, 0.10, z0), (x, 0.12, z1), 'GM_Wattle')
        for k, z in enumerate((z0 + 0.12, z0 + 0.35, z0 + 0.58)):
            m = Matrix.Translation((0, 0.02, z)) @ Matrix.Rotation(math.radians(8 if k % 2 else -6), 4, 'Y')
            b.box((-x - 0.06, 0, -0.06), (x + 0.06, 0.04, 0.06), 'GM_Plank', m)
    else:
        b.box((-x, 0.10, z0), (x, 0.12, z1), 'GM_Window')
        for side in (-1, 1):
            sx = side * (x + 0.08)
            b.box((min(sx, sx + side * 0.03), -0.3, z0), (max(sx, sx + side * 0.03), 0, z1), 'GM_Plank')
    return b


def wall_door():
    b = Builder()
    frame(b)
    x, top = 0.38, 2.05
    infill(b, -0.5, -x - 0.08, TIMBER, FLOOR_H - TIMBER)
    infill(b, x + 0.08, 0.5, TIMBER, FLOOR_H - TIMBER)
    infill(b, -x - 0.08, x + 0.08, top + 0.1, FLOOR_H - TIMBER)
    b.box((-x - 0.08, 0, TIMBER), (-x, TIMBER, top + 0.1), 'GM_Timber')
    b.box((x, 0, TIMBER), (x + 0.08, TIMBER, top + 0.1), 'GM_Timber')
    b.box((-x - 0.08, 0, top), (x + 0.08, TIMBER, top + 0.1), 'GM_Timber')
    b.box((-x, 0.08, TIMBER), (x, 0.13, top), 'GM_Plank')
    b.box((-0.45, -0.35, -PLINTH_H), (0.45, 0, -PLINTH_H + 0.17), 'GM_Stone')
    return b


def wall_plank():
    b = Builder()
    frame(b)
    for i in range(5):
        x0 = -0.5 + i * 0.2 + 0.005
        b.box((x0, 0.02, TIMBER), (x0 + 0.19, 0.07, FLOOR_H - TIMBER), 'GM_Plank')
    b.box((-0.5, 0.07, TIMBER), (0.5, WALL_T, FLOOR_H - TIMBER), 'GM_Plank')
    return b


def wall_broken():
    b = Builder()
    frame(b, mid_rail=True)
    hx0, hx1, hz0, hz1 = -0.25, 0.2, 0.55, 1.15
    infill(b, -0.5, hx0, TIMBER, 1.2)
    infill(b, hx1, 0.5, TIMBER, 1.2)
    infill(b, hx0, hx1, TIMBER, hz0)
    infill(b, -0.5, 0.5, 1.2 + TIMBER, FLOOR_H - TIMBER)
    b.box((hx0, 0.13, hz0), (hx1, 0.17, 1.2), 'GM_Wattle')
    return b


def post():
    b = Builder()
    b.box((-0.07, -0.02, 0), (0.07, TIMBER, FLOOR_H), 'GM_Timber')
    return b


def corner_post():
    b = Builder()
    b.box((-0.03, -0.03, 0), (0.2, 0.2, FLOOR_H), 'GM_Timber')
    return b


def beam_1():
    b = Builder()
    b.box((-0.5, 0, FLOOR_H - TIMBER), (0.5, TIMBER, FLOOR_H), 'GM_Timber')
    return b


def plinth():
    b = Builder()
    b.box((-0.5, -0.05, 0), (0.5, WALL_T + 0.05, PLINTH_H), 'GM_Stone')
    return b


def plinth_corner():
    b = Builder()
    b.box((-0.05, -0.05, 0), (0.25, 0.25, PLINTH_H + 0.02), 'GM_Stone')
    return b


def gable(depth):
    b = Builder()
    h, r = depth / 2, rise(depth)
    b.prism_xz([(-h, TIMBER), (h, TIMBER), (0, r)], INFILL_INSET, WALL_T, 'GM_Daub')
    b.box((-h, 0, 0), (h, TIMBER, TIMBER), 'GM_Timber')
    b.beam((-h, TIMBER / 2, TIMBER / 2), (0, TIMBER / 2, r), TIMBER, 'GM_Timber')
    b.beam((h, TIMBER / 2, TIMBER / 2), (0, TIMBER / 2, r), TIMBER, 'GM_Timber')
    b.beam((0, TIMBER / 2, TIMBER), (0, TIMBER / 2, r), TIMBER, 'GM_Timber')
    return b


def roof(depth, width=1.0, hole=False):
    """One cell of the gable roof along the ridge: both slopes and the ridge cap."""
    b = Builder()
    h, r = depth / 2, rise(depth)
    length = (h + EAVE) / math.cos(PITCH)
    w = width / 2
    eave = Matrix.Translation((0, -h - EAVE, -EAVE * math.tan(PITCH))) @ Matrix.Rotation(PITCH, 4, 'X')
    mirror = Matrix.Scale(-1, 4, (0, 1, 0))
    for side, m in ((0, eave), (1, mirror @ eave)):
        if hole and side == 0:
            a, c = length * 0.35, length * 0.7
            b.box((-w, 0, 0), (w, a, ROOF_T), 'GM_Roof', m)
            b.box((-w, c, 0), (w, length, ROOF_T), 'GM_Roof', m)
            b.box((-w, a, 0), (-0.12, c, ROOF_T), 'GM_Roof', m)
            for x in (-0.05, 0.3):
                b.box((x - 0.05, a, -0.1), (x + 0.05, c, 0), 'GM_Timber', m)
        else:
            b.box((-w, 0, 0), (w, length, ROOF_T), 'GM_Roof', m)
    top = r + ROOF_T / math.cos(PITCH)
    b.box((-w, -0.14, top - 0.12), (w, 0.14, top + 0.06), 'GM_Roof')
    return b


def chimney(depth):
    b = Builder()
    top = rise(depth) + 0.9
    b.box((-0.3, -0.3, 0), (0.3, 0.3, top), 'GM_Stone')
    b.box((-0.36, -0.36, top), (0.36, 0.36, top + 0.1), 'GM_Stone')
    return b


def stake():
    b = Builder()
    b.box((-0.03, -0.03, 0), (0.03, 0.03, 0.8), 'GM_Plank')
    return b


def rope_1():
    b = Builder()
    b.box((-0.5, -0.01, 0.55), (0.5, 0.01, 0.57), 'GM_Rope')
    return b


def scaffold_1():
    b = Builder()
    y, top, deck = -0.65, FLOOR_H + 0.6, FLOOR_H - 0.2
    for x in (-0.45, 0.45):
        b.box((x - 0.04, y - 0.04, 0), (x + 0.04, y + 0.04, top), 'GM_Plank')
        b.box((x - 0.03, y - 0.1, deck - 0.06), (x + 0.03, 0.05, deck), 'GM_Plank')
    b.box((-0.5, y - 0.03, deck - 0.12), (0.5, y + 0.03, deck - 0.06), 'GM_Plank')
    b.box((-0.5, -0.75, deck), (0.5, -0.25, deck + 0.05), 'GM_Plank')
    b.beam((-0.45, y, 0.2), (0.45, y, deck - 0.15), 0.06, 'GM_Plank')
    return b


def prop():
    b = Builder()
    b.beam((0, -1.1, 0), (0, -0.02, 1.9), 0.12, 'GM_Timber')
    return b


def module_builders():
    mods = {
        'wall_plain_a': lambda: wall_plain(brace=False),
        'wall_plain_b': lambda: wall_plain(brace=True),
        'wall_window': lambda: wall_window(boarded=False),
        'wall_window_boarded': lambda: wall_window(boarded=True),
        'wall_door': wall_door, 'wall_plank': wall_plank, 'wall_broken': wall_broken,
        'post': post, 'corner_post': corner_post, 'beam_1': beam_1, 'plinth': plinth, 'plinth_corner': plinth_corner,
        'stake': stake, 'rope_1': rope_1, 'scaffold_1': scaffold_1, 'prop': prop,
    }
    for d in DEPTHS:
        mods[f'gable_d{d}'] = lambda d=d: gable(d)
        mods[f'roof_d{d}'] = lambda d=d: roof(d)
        mods[f'roof_hole_d{d}'] = lambda d=d: roof(d, hole=True)
        mods[f'roof_ovh_d{d}'] = lambda d=d: roof(d, width=GABLE_OVH)
        mods[f'chimney_d{d}'] = lambda d=d: chimney(d)
    return mods


# ---------- scene ----------

def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    return bpy.context.scene


def build_kit(scene):
    coll = bpy.data.collections.new('Kit')
    scene.collection.children.link(coll)
    kit = {}
    for name, make in module_builders().items():
        obj = bpy.data.objects.new(name, make().finish(name))
        coll.objects.link(obj)
        obj.hide_render = True
        kit[name] = obj
    return kit


def export_fbx(kit, folder):
    os.makedirs(folder, exist_ok=True)
    layer = bpy.context.view_layer
    for name, obj in kit.items():
        for o in layer.objects:
            o.select_set(False)
        obj.select_set(True)
        layer.objects.active = obj
        bpy.ops.export_scene.fbx(
            filepath=os.path.join(folder, name + '.fbx'), use_selection=True, object_types={'MESH'},
            apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
            bake_space_transform=True, mesh_smooth_type='FACE', add_leaf_bones=False)
    print(f'exported {len(kit)} modules to {folder}')


class Town:
    """Places linked copies of kit modules, the same way the in-engine generator does."""

    def __init__(self, scene, kit):
        self.kit = kit
        self.coll = bpy.data.collections.new('Preview')
        scene.collection.children.link(self.coll)

    def put(self, name, x, y, z=0.0, rot=0.0):
        obj = bpy.data.objects.new(name, self.kit[name].data)
        obj.location = (x, y, z)
        obj.rotation_euler = (0, 0, math.radians(rot))
        self.coll.objects.link(obj)

    def sides(self, ox, oy, w, d):
        """Facade cells of the footprint: (x, y, rotation, is_front)."""
        for i in range(w):
            yield ox + i + 0.5, oy, 0, True
            yield ox + w - i - 0.5, oy + d, 180, False
        for j in range(d):
            yield ox, oy + d - j - 0.5, -90, False
            yield ox + w, oy + j + 0.5, 90, False

    def boundaries(self, ox, oy, w, d):
        for i in range(1, w):
            yield ox + i, oy, 0
            yield ox + i, oy + d, 180
        for j in range(1, d):
            yield ox, oy + j, -90
            yield ox + w, oy + j, 90

    def corners(self, ox, oy, w, d):
        return ((ox, oy, 0), (ox + w, oy, 90), (ox + w, oy + d, 180), (ox, oy + d, -90))

    def house(self, ox, oy, w, d, floors, seed, stage='ready', decay=0.0):
        rng = random.Random(seed)
        if stage == 'planned':
            for x, y, rot, _ in self.sides(ox, oy, w, d):
                self.put('rope_1', x, y, 0, rot)
            for x, y, _ in list(self.boundaries(ox, oy, w, d)) + list(self.corners(ox, oy, w, d)):
                self.put('stake', x, y)
            return
        for x, y, rot, _ in self.sides(ox, oy, w, d):
            self.put('plinth', x, y, 0, rot)
        for x, y, rot in self.corners(ox, oy, w, d):
            self.put('plinth_corner', x, y, 0, rot)
        built = 1 if stage == 'construction' else floors
        door = rng.randrange(w)
        for f in range(built):
            z = PLINTH_H + f * FLOOR_H
            for x, y, rot in self.corners(ox, oy, w, d):
                self.put('corner_post', x, y, z, rot)
            for x, y, rot in self.boundaries(ox, oy, w, d):
                self.put('post', x, y, z, rot)
            front_index = 0
            for x, y, rot, front in self.sides(ox, oy, w, d):
                if stage == 'construction':
                    self.put('beam_1', x, y, z, rot)
                    if rng.random() < 0.35:
                        self.put(rng.choice(('wall_plain_a', 'wall_plain_b')), x, y, z, rot)
                    if front or rot == 90:
                        self.put('scaffold_1', x, y, z, rot)
                    continue
                if front and f == 0 and front_index == door:
                    name = 'wall_door'
                elif rng.random() < (0.45 if front else 0.25) + (0.1 if f > 0 else 0):
                    name = 'wall_window_boarded' if rng.random() < decay * 0.7 else 'wall_window'
                else:
                    name = 'wall_broken' if rng.random() < decay * 0.5 else rng.choice(('wall_plain_a', 'wall_plain_b'))
                self.put(name, x, y, z, rot)
                if f == 0 and rng.random() < decay * 0.3:
                    self.put('prop', x, y, 0, rot)
                if front:
                    front_index += 1
        if stage == 'construction':
            return
        top = PLINTH_H + floors * FLOOR_H
        self.put(f'gable_d{d}', ox, oy + d / 2, top, -90)
        self.put(f'gable_d{d}', ox + w, oy + d / 2, top, 90)
        for i in range(w):
            hole = rng.random() < decay * 0.4
            self.put(f'roof_hole_d{d}' if hole else f'roof_d{d}', ox + i + 0.5, oy + d / 2, top)
        self.put(f'roof_ovh_d{d}', ox - GABLE_OVH / 2, oy + d / 2, top)
        self.put(f'roof_ovh_d{d}', ox + w + GABLE_OVH / 2, oy + d / 2, top)
        if rng.random() < 0.8:
            self.put(f'chimney_d{d}', ox + rng.randrange(w) + 0.5, oy + d / 2 + d / 4, top)


def setup_render(scene, target, distance, out):
    engines = {e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items}
    for engine in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE', 'CYCLES'):
        if engine in engines:
            scene.render.engine = engine
            break
    if scene.render.engine == 'CYCLES':
        scene.cycles.samples = 48
    else:
        scene.eevee.taa_render_samples = 32
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    scene.render.filepath = out
    try:
        scene.view_settings.view_transform = 'AgX'
    except TypeError:
        pass

    world = bpy.data.worlds.new('Overcast')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.5, 0.52, 0.56, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.55
    scene.world = world

    sun = bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN'))
    sun.data.energy = 3.0
    sun.data.angle = math.radians(8)
    sun.rotation_euler = (math.radians(50), 0, math.radians(-20))
    scene.collection.objects.link(sun)

    cam = bpy.data.objects.new('Camera', bpy.data.cameras.new('Camera'))
    cam.data.angle = math.radians(30)
    pitch, yaw = math.radians(55), math.radians(225)
    target = Vector(target)
    cam.location = target + distance * Vector((math.cos(pitch) * math.cos(yaw), math.cos(pitch) * math.sin(yaw), math.sin(pitch)))
    cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(cam)
    scene.camera = cam


def ground(scene, size, center):
    b = Builder()
    b.box((-size / 2, -size / 2, -0.1), (size / 2, size / 2, 0), 'GM_Ground')
    obj = bpy.data.objects.new('Ground', b.finish('Ground'))
    obj.location = (*center, 0)
    scene.collection.objects.link(obj)


def render_preview(out):
    scene = reset_scene()
    kit = build_kit(scene)
    town = Town(scene, kit)
    town.house(0, 0, 5, 4, 2, seed=1)
    town.house(7, 0, 3, 3, 1, seed=2)
    town.house(12, 0, 4, 4, 1, seed=3, stage='construction')
    town.house(0, 7, 4, 5, 1, seed=4)
    town.house(6.5, 7, 4, 3, 1, seed=5, stage='planned')
    town.house(12, 7, 4, 4, 2, seed=6, decay=0.7)
    ground(scene, 60, (8, 6))
    setup_render(scene, (8, 6, 1), 62, out)
    bpy.ops.render.render(write_still=True)
    print('preview', out)


def render_sheet(out):
    scene = reset_scene()
    kit = build_kit(scene)
    town = Town(scene, kit)
    names = list(kit)
    cols = 8
    for i, name in enumerate(names):
        x, y = (i % cols) * 4.5, -(i // cols) * 6
        town.put(name, x, y)
    rows = (len(names) + cols - 1) // cols
    center = ((cols - 1) * 4.5 / 2, -(rows - 1) * 6 / 2)
    ground(scene, 80, center)
    setup_render(scene, (*center, 0.5), 95, out)
    bpy.ops.render.render(write_still=True)
    print('sheet', out, 'modules:', ', '.join(names))


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    p = argparse.ArgumentParser()
    p.add_argument('--fbx')
    p.add_argument('--preview')
    p.add_argument('--sheet')
    args = p.parse_args(argv)
    if args.fbx:
        scene = reset_scene()
        export_fbx(build_kit(scene), args.fbx)
    if args.preview:
        render_preview(args.preview)
    if args.sheet:
        render_sheet(args.sheet)


main()
