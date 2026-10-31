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
TIMBER = 0.16
ROOF_RISE = 2.8
EAVE = 0.35
GABLE_OVH = 0.3
ROOF_T = 0.09
THATCH_T = 0.35
THATCH_EAVE = 0.2
JETTY = 0.5
# Slates: a grid of TILE_W across the roof in house coordinates, rows TILE_EXPOSE apart up the slope, each slate TILE_L long,
# so every row covers the upper part of the row below. Odd rows are offset by half a slate.
TILE_W = 0.25
TILE_L = 0.38
TILE_EXPOSE = 0.2
TILE_T = 0.02
# Lower corners of each slate are cut at 45 degrees by this much, as on hand-cut slates.
TILE_CHAMFER = 0.04
ROOF_VARIANTS = ('', '_b', '_c')
DEPTHS = (3, 4, 5, 6, 7, 8)
INFILL_INSET = 0.03
# Doors are 0.9 m wide and 2.0 m high in the clear. Between posts a cell has 0.8 m, so a door module spans two cells and stands
# on the boundary between them, with no post there.
DOOR_HALF = 0.45
DOOR_H = 2.0
DOOR_CELLS = 2

# Chamfer on box edges by material: a lit edge is what makes timber and stone read as solid at a distance.
BEVEL = {'GM_Timber': 0.015, 'GM_Stone': 0.03, 'GM_Rock': 0.025, 'GM_Plank': 0.006, 'GM_Door': 0.006, 'GM_Sash': 0.004}

# Field stones get a rounded edge, not a flat chamfer: a flat 45-degree strip on every stone reads as moulded tiles.
BEVEL_SEGMENTS = {'GM_Rock': 3}

# Wood grain follows the longest side of each box: in V for textures with vertical grain (timber, door boards), in U for the
# plank texture whose boards run horizontally. Other materials are projected along the world axes in finish().
GRAIN = {'GM_Timber': 'v', 'GM_Door': 'v', 'GM_Plank': 'u', 'GM_Roof': 'v', 'GM_Sash': 'v'}

MATERIALS = {
    'GM_Timber': (0.16, 0.11, 0.07),
    'GM_Daub': (0.62, 0.58, 0.50),
    'GM_Stone': (0.36, 0.35, 0.33),
    'GM_Rock': (0.38, 0.35, 0.31),
    'GM_Mortar': (0.30, 0.28, 0.25),
    'GM_Door': (0.28, 0.24, 0.20),
    'GM_Lead': (0.12, 0.12, 0.13),
    'GM_Sash': (0.48, 0.45, 0.41),
    'GM_Soot': (0.04, 0.035, 0.03),
    'GM_Grime': (0.20, 0.15, 0.10),
    'GM_Roof': (0.20, 0.20, 0.21),
    'GM_Thatch': (0.36, 0.33, 0.27),
    'GM_Plank': (0.30, 0.25, 0.19),
    'GM_Window': (0.12, 0.14, 0.13),
    'GM_Rope': (0.45, 0.38, 0.25),
    'GM_Wattle': (0.22, 0.17, 0.11),
    'GM_Ground': (0.27, 0.24, 0.18),
}


def roof_pitch(depth):
    """Roof pitch for a house of this depth: every roof rises ROOF_RISE above the wall top, a storey or a little more,
    so narrow houses get steep roofs and deep ones shallower roofs."""
    return math.atan2(ROOF_RISE, depth / 2)


class Builder:
    """Collects boxes, beams and prisms into one mesh with named material slots."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []
        self.uv = self.bm.loops.layers.uv.new('UVMap')
        self.mapped = set()

    def mat(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def box(self, lo, hi, mat, m=None, bevel=None, hole=None):
        """Box between two corners, bevelled by material. hole = (half width, depth, material): a square well cut down from the
        middle of the top face, its walls and bottom in that material; the box stays one piece with unbroken bevels."""
        (x0, y0, z0), (x1, y1, z1) = lo, hi
        corners = [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0), (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)]
        before = set(self.bm.faces)
        vs = [self.bm.verts.new(m @ Vector(c) if m else Vector(c)) for c in corners]
        idx = self.mat(mat)
        for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            self.bm.faces.new([vs[i] for i in f]).material_index = idx
        size = BEVEL.get(mat, 0.0) if bevel is None else bevel
        size = min(size, 0.3 * min(abs(x1 - x0), abs(y1 - y0), abs(z1 - z0)))
        if size > 0.001:
            edges = {e for v in vs for e in v.link_edges}
            bmesh.ops.bevel(self.bm, geom=list(edges), offset=size, segments=BEVEL_SEGMENTS.get(mat, 1), affect='EDGES', profile=0.5,
                            clamp_overlap=True)
            # Bevel faces get the mesh's first material, not the box's: in a module of several materials a stone would be
            # edged in mortar or plaster.
            for f in set(self.bm.faces) - before:
                f.material_index = idx
        if mat in GRAIN:
            self.grain_uv(set(self.bm.faces) - before, (x1 - x0, y1 - y0, z1 - z0), m, GRAIN[mat])
        if hole:
            self.well(set(self.bm.faces) - before, *hole)

    def well(self, faces, half, depth, mat):
        """Cut a square well into the topmost face of these faces: inset to the well's size, then pushed down by its depth."""
        for f in faces:
            f.normal_update()
        top = max((f for f in faces if f.normal.z > 0.99), key=lambda f: f.calc_center_median().z)
        c = top.calc_center_median()
        span = max(abs(v.co.x - c.x) for v in top.verts)
        bmesh.ops.inset_region(self.bm, faces=[top], thickness=span - half, depth=0, use_even_offset=True)
        res = bmesh.ops.extrude_face_region(self.bm, geom=[top])
        idx = self.mat(mat)
        for g in res['geom']:
            if isinstance(g, bmesh.types.BMVert):
                g.co.z -= depth
        self.bm.faces.remove(top)
        # The well's bottom and the walls standing on its edges.
        for bottom in (g for g in res['geom'] if isinstance(g, bmesh.types.BMFace)):
            for f in {bottom} | {f for e in bottom.edges for f in e.link_faces}:
                f.material_index = idx

    def grain_uv(self, faces, size, m, grain):
        """UV of one box in its own axes: the grain runs along the box's longest side, so a beam shows wood fibres along its length
        at any angle. The box's position shifts the texture, so neighbouring pieces do not repeat the same patch."""
        inv = m.inverted() if m else Matrix.Identity(4)
        rot = inv.to_3x3()
        long = max(range(3), key=lambda k: abs(size[k]))
        o = m.translation if m else Vector((0, 0, 0))
        # Large factors so that two pieces a few centimetres apart (neighbouring slates) still get unrelated patches of the texture.
        shift_u = (o.x * 4.73 + o.y * 2.31 + o.z * 3.97) % 2.0
        shift_v = (o.x * 3.11 + o.y * 5.17 + o.z * 1.93) % 2.0
        for f in faces:
            if f in self.mapped:
                continue
            f.normal_update()
            n = rot @ f.normal
            axis = max(range(3), key=lambda k: abs(n[k]))
            a, b = [k for k in range(3) if k != axis]
            across, along = (b, a) if a == long else (a, b)
            for loop in f.loops:
                c = inv @ loop.vert.co
                u, v = c[across] + shift_u, c[along] + shift_v
                loop[self.uv].uv = (v, u) if grain == 'u' else (u, v)
            self.mapped.add(f)

    def taper(self, cx, cy, s0, s1, z0, z1, mat):
        """Square frustum: side s0 at z0 narrowing (or widening) to s1 at z1."""
        h0, h1 = s0 / 2, s1 / 2
        vs = [self.bm.verts.new((cx + x * h, cy + y * h, z)) for h, z in ((h0, z0), (h1, z1)) for x, y in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
        idx = self.mat(mat)
        for f in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4), (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
            self.bm.faces.new([vs[i] for i in f]).material_index = idx

    def slab(self, pts, thickness, mat, m):
        """Convex outline in the XY plane, given counter-clockwise, extruded along Z by the thickness, then moved by m.
        Wood-like materials get the same per-piece grain UV as a box."""
        before = set(self.bm.faces)
        low = [self.bm.verts.new(m @ Vector((x, y, 0))) for x, y in pts]
        high = [self.bm.verts.new(m @ Vector((x, y, thickness))) for x, y in pts]
        idx = self.mat(mat)
        self.bm.faces.new(list(reversed(low))).material_index = idx
        self.bm.faces.new(high).material_index = idx
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            self.bm.faces.new([low[i], low[j], high[j], high[i]]).material_index = idx
        if mat in GRAIN:
            xs, ys = [x for x, _ in pts], [y for _, y in pts]
            self.grain_uv(set(self.bm.faces) - before, (max(xs) - min(xs), max(ys) - min(ys), thickness), m, GRAIN[mat])

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

    def prism_yz(self, pts, x0, x1, mat, m=None):
        """Convex polygon given in the YZ plane, extruded along X from x0 to x1, then moved by m."""
        tr = (lambda c: m @ Vector(c)) if m else Vector
        left = [self.bm.verts.new(tr((x0, y, z))) for y, z in pts]
        right = [self.bm.verts.new(tr((x1, y, z))) for y, z in pts]
        idx = self.mat(mat)
        self.bm.faces.new(left).material_index = idx
        self.bm.faces.new(list(reversed(right))).material_index = idx
        n = len(pts)
        for i in range(n):
            j = (i + 1) % n
            self.bm.faces.new([left[i], left[j], right[j], right[i]]).material_index = idx

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        uv = self.uv
        for face in self.bm.faces:
            if face in self.mapped:
                continue
            n = face.normal
            axis = max(range(3), key=lambda k: abs(n[k]))
            # On a slope the height is divided by the horizontal share of the normal: V runs along the slope in metres, not stretched.
            slope = max((1 - n.z * n.z) ** 0.5, 1e-4)
            for loop in face.loops:
                c = loop.vert.co
                loop[uv].uv = (c.y, c.z / slope) if axis == 0 else (c.x, c.z / slope) if axis == 1 else (c.x, c.y)
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

def frame(b, mid_rail=False, brace=0, cross=False):
    """Sill and head beams of a cell; a rail at 1.2 m; brace = +1 or -1: a long brace whose top leans to +X or -X; cross: a St Andrew's
    cross in the panel under the rail."""
    b.box((-0.5, 0, 0), (0.5, TIMBER, TIMBER), 'GM_Timber')
    b.box((-0.5, 0, FLOOR_H - TIMBER), (0.5, TIMBER, FLOOR_H), 'GM_Timber')
    if mid_rail:
        b.box((-0.5, 0, 1.2), (0.5, TIMBER, 1.2 + TIMBER), 'GM_Timber')
    if brace:
        b.beam((-0.42 * brace, TIMBER / 2, TIMBER), (0.42 * brace, TIMBER / 2, FLOOR_H - TIMBER), TIMBER * 0.85, 'GM_Timber')
    if cross:
        for s in (-1, 1):
            b.beam((-0.4 * s, TIMBER / 2, TIMBER), (0.4 * s, TIMBER / 2, 1.2), TIMBER * 0.7, 'GM_Timber')


def infill(b, x0, x1, z0, z1, mat='GM_Daub'):
    if x1 > x0 and z1 > z0:
        b.box((x0, INFILL_INSET, z0), (x1, WALL_T, z1), mat)


def wall_plain(brace=0, cross=False):
    b = Builder()
    frame(b, mid_rail=not brace, brace=brace, cross=cross)
    infill(b, -0.5, 0.5, TIMBER, FLOOR_H - TIMBER)
    return b


def window_cut(b, x=0.28, z0=0.95, z1=1.65):
    """Infill around a window opening of half width x from z0 to z1, the timber frame of the opening and a sill board under it."""
    infill(b, -0.5, -x, TIMBER, FLOOR_H - TIMBER)
    infill(b, x, 0.5, TIMBER, FLOOR_H - TIMBER)
    infill(b, -x, x, TIMBER, z0)
    infill(b, -x, x, z1, FLOOR_H - TIMBER)
    t = 0.08
    b.box((-x - t, 0, z0 - t), (-x, TIMBER * 0.8, z1 + t), 'GM_Timber')
    b.box((x, 0, z0 - t), (x + t, TIMBER * 0.8, z1 + t), 'GM_Timber')
    b.box((-x, 0, z0 - t), (x, TIMBER * 0.8, z0), 'GM_Timber')
    b.box((-x, 0, z1), (x, TIMBER * 0.8, z1 + t), 'GM_Timber')
    b.box((-x - 0.12, -0.07, z0 - t - 0.05), (x + 0.12, TIMBER * 0.8, z0 - t), 'GM_Plank')
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
        zm = (z0 + z1) / 2
        b.box((-0.02, 0.08, z0), (0.02, 0.12, z1), 'GM_Timber')
        b.box((-x, 0.08, zm - 0.02), (x, 0.12, zm + 0.02), 'GM_Timber')
        # Small panes between thin wooden bars: one down the middle of each half and one across each quarter.
        for lx in (-x / 2, x / 2):
            b.box((lx - 0.004, 0.095, z0), (lx + 0.004, 0.10, z1), 'GM_Sash', bevel=0)
        for lz in ((z0 + zm) / 2, (zm + z1) / 2):
            b.box((-x, 0.095, lz - 0.004), (x, 0.10, lz + 0.004), 'GM_Sash', bevel=0)
        for side in (-1, 1):
            sx = side * (x + 0.08)
            b.box((min(sx, sx + side * 0.03), -0.3, z0), (max(sx, sx + side * 0.03), 0, z1), 'GM_Plank')
    return b


def sash(b, x, z0, z1, y, across, down, f=0.0):
    """Glass of an opening with wooden bars in front of it: `across` bars down the window, `down` bars across it, and a frame of
    width f round the glass if f > 0. y is the front of the glass."""
    b.box((-x, y, z0), (x, y + 0.02, z1), 'GM_Window')
    if f > 0:
        b.box((-x, y - 0.03, z0), (-x + f, y + 0.02, z1), 'GM_Sash')
        b.box((x - f, y - 0.03, z0), (x, y + 0.02, z1), 'GM_Sash')
        b.box((-x + f, y - 0.03, z0), (x - f, y + 0.02, z0 + f), 'GM_Sash')
        b.box((-x + f, y - 0.03, z1 - f), (x - f, y + 0.02, z1), 'GM_Sash')
    ix, iz0, iz1 = x - f, z0 + f, z1 - f
    for i in range(1, across + 1):
        lx = -ix + 2 * ix * i / (across + 1)
        b.box((lx - 0.01, y - 0.015, iz0), (lx + 0.01, y, iz1), 'GM_Sash')
    for i in range(1, down + 1):
        lz = iz0 + (iz1 - iz0) * i / (down + 1)
        b.box((-ix, y - 0.015, lz - 0.01), (ix, y, lz + 0.01), 'GM_Sash')


def wall_window_closed():
    """The usual window with both shutter leaves shut: vertical boards with two battens on each leaf, darkness in the gap between them."""
    b = Builder()
    frame(b)
    x, z0, z1 = window_cut(b)
    b.box((-x, 0.10, z0), (x, 0.12, z1), 'GM_Soot')
    for side in (-1, 1):
        lo, hi = sorted((side * 0.004, side * x))
        b.box((lo, 0.03, z0), (hi, 0.07, z1), 'GM_Door')
        for z in (z0 + 0.1, z1 - 0.18):
            b.box((lo + 0.03, 0.0, z), (hi - 0.03, 0.03, z + 0.08), 'GM_Door')
    return b


def wall_window_ribbon():
    """Ribbon window high in the wall: a low band of five lights between timber mullions, its frame against the posts. Cells side by
    side make one long band."""
    b = Builder()
    frame(b)
    x, z0, z1 = window_cut(b, 0.35, 1.5, 1.85)
    b.box((-x, 0.10, z0), (x, 0.12, z1), 'GM_Window')
    for i in range(1, 5):
        lx = -x + 2 * x * i / 5
        b.box((lx - 0.02, 0.08, z0), (lx + 0.02, 0.12, z1), 'GM_Timber')
    return b


def wall_window_hood():
    """Small square window under a board hood on two brackets."""
    b = Builder()
    frame(b)
    x, z0, z1 = window_cut(b, 0.2, 1.15, 1.55)
    sash(b, x, z0, z1, 0.10, 1, 1)
    zh, run, tilt = z1 + 0.22, 0.34, math.radians(25)
    m = Matrix.Translation((0, 0, zh)) @ Matrix.Rotation(tilt, 4, 'X')
    b.box((-x - 0.18, -run, 0), (x + 0.18, 0, 0.025), 'GM_Plank', m)
    reach = 0.24
    for side in (-1, 1):
        bx = side * (x + 0.1)
        b.prism_yz([(0, zh - 0.28), (-reach, zh - reach * math.tan(tilt)), (0, zh)], bx - 0.025, bx + 0.025, 'GM_Timber')
    return b


def wall_window_small():
    """Small window for end walls and upper floors: one bar down and one across."""
    b = Builder()
    frame(b)
    x, z0, z1 = window_cut(b, 0.14, 1.3, 1.62)
    sash(b, x, z0, z1, 0.10, 1, 1)
    return b


def wall_door():
    """Timber door over two cells (X from -1 to 1), centred on the cell boundary: an opening DOOR_HALF * 2 wide and DOOR_H high above
    the sill beam between two full-height jambs, a lintel up to the head beam, and on each side a field with a rail at 1.2 m."""
    b = Builder()
    half, x, j = DOOR_CELLS / 2, DOOR_HALF, TIMBER * 0.8
    top = TIMBER + DOOR_H
    b.box((-half, 0, 0), (half, TIMBER, TIMBER), 'GM_Timber')
    b.box((-half, 0, FLOOR_H - TIMBER), (half, TIMBER, FLOOR_H), 'GM_Timber')
    for side in (-1, 1):
        lo, hi = sorted((side * x, side * (x + j)))
        b.box((lo, 0, TIMBER), (hi, TIMBER, FLOOR_H - TIMBER), 'GM_Timber')
        lo, hi = sorted((side * (x + j), side * half))
        b.box((lo, 0, 1.2), (hi, TIMBER, 1.2 + TIMBER), 'GM_Timber')
        infill(b, lo, hi, TIMBER, 1.2)
        infill(b, lo, hi, 1.2 + TIMBER, FLOOR_H - TIMBER)
    b.box((-x, 0, top), (x, TIMBER, FLOOR_H - TIMBER), 'GM_Timber')
    b.box((-x, 0.08, TIMBER), (x, 0.13, top), 'GM_Door')
    for z in (TIMBER + 0.3, top - 0.35):
        b.box((-x + 0.04, 0.05, z), (x - 0.04, 0.08, z + 0.12), 'GM_Door')
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
    b.box((-TIMBER / 2, -0.02, 0), (TIMBER / 2, TIMBER, FLOOR_H), 'GM_Timber')
    return b


def corner_post():
    b = Builder()
    b.box((-0.03, -0.03, 0), (TIMBER + 0.06, TIMBER + 0.06, FLOOR_H), 'GM_Timber')
    return b


def beam_1():
    b = Builder()
    b.box((-0.5, 0, FLOOR_H - TIMBER), (0.5, TIMBER, FLOOR_H), 'GM_Timber')
    return b


def stone_course(b, rng, x0, x1, z0, z1, y0, y1, offset, gap=None):
    """One course of stones of uneven length and depth between x0 and x1, cut back from an opening gap = (g0, g1)."""
    x = x0 - offset
    while x < x1:
        length = rng.uniform(0.18, 0.46)
        a, c = max(x, x0), min(x + length, x1)
        if gap and a < gap[1] and c > gap[0]:
            # A stone over the opening keeps only its longer part outside it.
            a, c = (a, gap[0]) if gap[0] - a > c - gap[1] else (gap[1], c)
        if c - a > 0.05:
            out = rng.uniform(0.0, 0.035)
            b.box((a + rng.uniform(0.006, 0.014), y0 - out, z0 + rng.uniform(0, 0.025)),
                  (c - rng.uniform(0.006, 0.014), y1, z1 - rng.uniform(0, 0.025)), 'GM_Rock')
        x += length


def plinth():
    """Two courses of stones in front of a recessed mortar core."""
    b = Builder()
    rng = random.Random(41)
    b.box((-0.5, -0.02, 0), (0.5, WALL_T + 0.05, PLINTH_H), 'GM_Mortar', bevel=0)
    half = PLINTH_H / 2
    stone_course(b, rng, -0.5, 0.5, 0, half, -0.05, WALL_T, 0.0)
    stone_course(b, rng, -0.5, 0.5, half, PLINTH_H, -0.05, WALL_T, 0.17)
    return b


def plinth_cellar():
    """Plinth cell with a small barred cellar window under a lintel stone."""
    b = Builder()
    rng = random.Random(43)
    x0, x1, z0, z1 = -0.2, 0.2, 0.06, 0.26
    b.box((-0.5, -0.02, 0), (x0, WALL_T + 0.05, PLINTH_H), 'GM_Mortar', bevel=0)
    b.box((x1, -0.02, 0), (0.5, WALL_T + 0.05, PLINTH_H), 'GM_Mortar', bevel=0)
    b.box((x0, -0.02, 0), (x1, WALL_T + 0.05, z0), 'GM_Mortar', bevel=0)
    b.box((x0, 0.16, z0), (x1, 0.18, z1), 'GM_Lead')
    half = PLINTH_H / 2
    stone_course(b, rng, -0.5, 0.5, 0, half, -0.05, WALL_T, 0.0, (x0 - 0.04, x1 + 0.04))
    stone_course(b, rng, -0.5, 0.5, half, PLINTH_H, -0.05, WALL_T, 0.17, (x0 - 0.1, x1 + 0.1))
    b.box((x0 - 0.1, -0.05, z1), (x1 + 0.1, WALL_T, PLINTH_H), 'GM_Rock')
    b.box((x0 - 0.04, -0.06, z0 - 0.05), (x1 + 0.04, WALL_T, z0), 'GM_Rock')
    for i in range(1, 5):
        x = x0 + (x1 - x0) * i / 5
        b.box((x - 0.01, 0.0, z0), (x + 0.01, 0.02, z1), 'GM_Lead')
    return b


def plinth_corner():
    b = Builder()
    b.box((-0.06, -0.06, 0), (0.25, 0.25, PLINTH_H / 2), 'GM_Rock')
    b.box((-0.05, -0.05, PLINTH_H / 2 + 0.008), (0.25, 0.25, PLINTH_H + 0.02), 'GM_Rock')
    return b


# ---------- ground floor of stone or plaster: no timber frame, dressed stones at the corners and around openings ----------

def solid_wall(b, mat, hole=None):
    """Full face of one cell from floor to floor, with an optional hole (x0, x1, z0, z1) left for an opening. No chamfer: the pieces
    of one face must meet without a seam."""
    if hole is None:
        b.box((-0.5, 0, 0), (0.5, WALL_T, FLOOR_H), mat, bevel=0)
        return
    x0, x1, z0, z1 = hole
    b.box((-0.5, 0, 0), (x0, WALL_T, FLOOR_H), mat, bevel=0)
    b.box((x1, 0, 0), (0.5, WALL_T, FLOOR_H), mat, bevel=0)
    if z0 > 0:
        b.box((x0, 0, 0), (x1, WALL_T, z0), mat, bevel=0)
    b.box((x0, 0, z1), (x1, WALL_T, FLOOR_H), mat, bevel=0)


def ground_plain(mat):
    b = Builder()
    solid_wall(b, mat)
    return b


def ground_window(mat):
    """Small window in a dressed stone surround: sill, lintel and two stones a side, glass in a wooden frame with glazing bars set
    back in the wall."""
    b = Builder()
    x, z0, z1 = 0.22, 1.0, 1.55
    solid_wall(b, mat, (-0.33, 0.33, z0 - 0.08, z1 + 0.15))
    # Mortar bed behind the side stones, a little back from their faces: the joints show mortar, not a slit through the wall.
    for side in (-1, 1):
        lo, hi = sorted((side * x, side * 0.33))
        b.box((lo, 0.0, z0 - 0.08), (hi, WALL_T, z1 + 0.15), 'GM_Mortar', bevel=0)
    b.box((-0.35, -0.045, z0 - 0.08), (0.35, WALL_T, z0), 'GM_Rock')
    b.box((-0.34, -0.025, z1), (0.34, WALL_T, z1 + 0.15), 'GM_Rock')
    for side in (-1, 1):
        split = z0 + (0.3 if side < 0 else 0.25)
        for (za, zb), wide in (((z0, split), side < 0), ((split, z1), side > 0)):
            outer = 0.33 if wide else 0.31
            lo, hi = sorted((side * x, side * outer))
            b.box((lo, -0.02, za + 0.005), (hi, WALL_T, zb - 0.005), 'GM_Rock')
    b.box((-x, 0.13, z0), (x, 0.15, z1), 'GM_Window')
    # Frame round the glass, then three bars down and two across inside it.
    f, y0, y1 = 0.04, 0.11, 0.16
    b.box((-x, y0, z0), (-x + f, y1, z1), 'GM_Sash')
    b.box((x - f, y0, z0), (x, y1, z1), 'GM_Sash')
    b.box((-x + f, y0, z0), (x - f, y1, z0 + f), 'GM_Sash')
    b.box((-x + f, y0, z1 - f), (x - f, y1, z1), 'GM_Sash')
    ix, iz0, iz1 = x - f, z0 + f, z1 - f
    for lx in (-ix / 2, 0, ix / 2):
        b.box((lx - 0.01, 0.115, iz0), (lx + 0.01, 0.13, iz1), 'GM_Sash')
    for lz in (iz0 + (iz1 - iz0) / 3, iz0 + 2 * (iz1 - iz0) / 3):
        b.box((-ix, 0.115, lz - 0.01), (ix, 0.13, lz + 0.01), 'GM_Sash')
    return b


def ground_window_small(mat):
    """Small square window in a dressed stone surround: sill, lintel and one stone a side, glass in a wooden frame with a cross."""
    b = Builder()
    x, z0, z1 = 0.12, 1.25, 1.55
    solid_wall(b, mat, (-0.2, 0.2, z0 - 0.07, z1 + 0.12))
    for side in (-1, 1):
        lo, hi = sorted((side * x, side * 0.2))
        b.box((lo, 0.0, z0 - 0.07), (hi, WALL_T, z1 + 0.12), 'GM_Mortar', bevel=0)
        lo, hi = sorted((side * x, side * 0.19))
        b.box((lo, -0.02, z0 + 0.005), (hi, WALL_T, z1 - 0.005), 'GM_Rock')
    b.box((-0.22, -0.04, z0 - 0.07), (0.22, WALL_T, z0), 'GM_Rock')
    b.box((-0.21, -0.025, z1), (0.21, WALL_T, z1 + 0.12), 'GM_Rock')
    sash(b, x, z0, z1, 0.13, 1, 1, 0.03)
    return b


def arc(radius, cz, a0, a1, steps):
    return [(radius * math.cos(math.radians(a)), cz + radius * math.sin(math.radians(a)))
            for a in (a0 + (a1 - a0) * i / steps for i in range(steps + 1))]


def ground_door(mat):
    """Round-arched door over two cells (X from -1 to 1), centred on the cell boundary: DOOR_HALF * 2 wide, the arch springing at
    DOOR_H - DOOR_HALF so the top of the opening is DOOR_H. A dressed stone surround: jamb stones, a ring of wedge stones with a
    keystone, a plank door with battens and iron strap hinges."""
    b = Builder()
    half = DOOR_CELLS / 2
    r, ring = DOOR_HALF, 0.13
    spring = DOOR_H - r
    big = r + ring
    jamb_long, jamb_short = r + 0.14, r + 0.09
    b.box((-half, 0, spring + big), (half, WALL_T, FLOOR_H), mat, bevel=0)
    for side in (-1, 1):
        lo, hi = sorted((side * jamb_short, side * half))
        b.box((lo, 0, 0), (hi, WALL_T, spring), mat, bevel=0)
        # Mortar behind the jamb stones, set back from their faces: the joints between stones read as mortar, not as slits
        # through the wall.
        lo, hi = sorted((side * (r + 0.005), side * jamb_short))
        b.box((lo, 0, 0), (hi, WALL_T, spring), 'GM_Mortar', bevel=0)
        # Spandrel: the wall between the outside of the arch and the corner of the module, as a fan of triangles from that corner.
        corner = (side * half, spring + big)
        pts = [(side * half, spring)] + [(side * px, pz) for px, pz in arc(big, spring, 0, 90, 6)]
        for a, c in zip(pts, pts[1:]):
            b.prism_xz([corner, a, c], 0, WALL_T, mat)
        z, k = 0.0, 0 if side < 0 else 1
        while z < spring - 0.01:
            hgt = min(0.32, spring - z)
            outer = jamb_long if k % 2 == 0 else jamb_short
            lo, hi = sorted((side * r, side * outer))
            b.box((lo, -0.02, z + 0.006), (hi, WALL_T, z + hgt - 0.006), 'GM_Rock')
            z += hgt
            k += 1
    # The same mortar behind the arch stones.
    inner, outer = arc(r + 0.005, spring, 0, 180, 14), arc(big - 0.005, spring, 0, 180, 14)
    for i in range(len(inner) - 1):
        b.prism_xz([inner[i], inner[i + 1], outer[i + 1], outer[i]], 0, WALL_T, 'GM_Mortar')
    count = 7
    for i in range(count):
        a0, a1 = 180 * i / count + 0.8, 180 * (i + 1) / count - 0.8
        outer = big + (0.05 if i == count // 2 else 0)
        inner = arc(r, spring, a0, a1, 2)
        outside = arc(outer, spring, a1, a0, 2)
        b.prism_xz(inner + outside, -0.02, WALL_T, 'GM_Rock')
    # The leaf runs a little behind the stones, so no light shows between it and the surround.
    leaf = [(-r - 0.01, 0), (r + 0.01, 0)] + arc(r + 0.01, spring, 0, 180, 12)
    b.prism_xz(leaf, 0.10, 0.15, 'GM_Door')
    for z in (0.25, 0.85, 1.45):
        b.box((-r + 0.05, 0.07, z), (r - 0.05, 0.10, z + 0.11), 'GM_Door')
        b.box((-r + 0.03, 0.062, z + 0.03), (-r + 0.5, 0.07, z + 0.08), 'GM_Lead')
    b.box((r - 0.16, 0.04, 0.98), (r - 0.12, 0.07, 1.04), 'GM_Lead')
    b.box((-r, -0.02, -0.02), (r, WALL_T, 0.01), 'GM_Rock')
    return b


def quoin():
    """Dressed corner stones of a stone or plaster floor, long and short in turn on each face. Pivot as for the corner post."""
    b = Builder()
    z, k = 0.0, 0
    while z < FLOOR_H - 0.01:
        hgt = min(0.26, FLOOR_H - z)
        lx, ly = (0.42, 0.25) if k % 2 == 0 else (0.25, 0.42)
        b.box((-0.025, -0.025, z + 0.006), (lx, ly, z + hgt - 0.006), 'GM_Rock')
        z += hgt
        k += 1
    return b


def steps():
    """Stone steps up to a door: a landing level with the floor and one step below, each of two slabs. Facade module at ground level."""
    b = Builder()
    top = PLINTH_H
    b.box((-0.54, -0.5, 0), (0.54, -0.01, top - 0.07), 'GM_Mortar', bevel=0)
    b.box((-0.58, -0.54, top - 0.075), (0.02, 0.0, top), 'GM_Rock')
    b.box((0.035, -0.52, top - 0.08), (0.6, 0.0, top - 0.004), 'GM_Rock')
    b.box((-0.68, -0.88, 0), (-0.04, -0.5, top / 2), 'GM_Rock')
    b.box((-0.025, -0.86, 0), (0.67, -0.5, top / 2 - 0.012), 'GM_Rock')
    return b


def door_grime(x, y):
    """Dust and mud at the foot of a door: a thin sheet just in front of the door leaf, fading upwards in its own material."""
    b = Builder()
    b.box((-x, y - 0.002, 0), (x, y, 0.45), 'GM_Grime', bevel=0)
    return b


def gable(depth):
    b = Builder()
    h, r, p = depth / 2, ROOF_RISE, roof_pitch(depth)
    # The infill stops where the underside of the roof meets the top of the beam, not above the eave: a thick beam would otherwise
    # lift its edge through the roof boards.
    foot = h - TIMBER / math.tan(p)
    b.prism_xz([(-foot, TIMBER), (foot, TIMBER), (0, r)], INFILL_INSET, WALL_T, 'GM_Daub')
    b.box((-h, 0, 0), (h, TIMBER, TIMBER), 'GM_Timber')
    # Gable rafters sit under the roof boards, not centred on the roof line, or they show through the thin shingle roof.
    drop = TIMBER / 2 / math.cos(p) + 0.01
    b.beam((-h, TIMBER / 2, TIMBER / 2 - drop), (0, TIMBER / 2, r - drop), TIMBER, 'GM_Timber')
    b.beam((h, TIMBER / 2, TIMBER / 2 - drop), (0, TIMBER / 2, r - drop), TIMBER, 'GM_Timber')
    b.beam((0, TIMBER / 2, TIMBER), (0, TIMBER / 2, r), TIMBER, 'GM_Timber')
    # Struts in a V: from the foot of the king post up and out to the rafters, ending on the rafter's centre line.
    xe = h * 0.45
    ze = r - drop - xe * (r - TIMBER / 2) / h - 0.03
    for s in (-1, 1):
        b.beam((s * TIMBER / 2, TIMBER / 2, TIMBER), (s * xe, TIMBER / 2, ze), TIMBER * 0.8, 'GM_Timber')
    return b


def slate_rows(b, m, x_lo, x_hi, length, rng, grid=0.0, outer=None, base=ROOF_T, skip=None):
    """Rows of slates on one slope, in its frame m: X along the ridge, Y up the slope from the eave, Z out of the roof.
    A slate belongs to the module whose range [x_lo, x_hi) holds its centre, so cells laid side by side meet without gaps or doubles.
    grid is where the module's x = 0 falls on the slate grid. On the outer edge of an end module the slates that hang over it are
    added too, cut flush with the edge."""
    tilt = math.atan2(TILE_T, TILE_L)
    row, s0 = 0, -0.06
    while s0 < length - 0.04:
        off = 0.0 if row % 2 == 0 else TILE_W / 2
        k = math.floor((x_lo - TILE_W + grid - off) / TILE_W)
        while True:
            c = k * TILE_W + off - grid
            k += 1
            if c - TILE_W / 2 >= x_hi + TILE_W:
                break
            a, e = c - TILE_W / 2, c + TILE_W / 2
            cut_a = cut_e = False
            if x_lo <= c < x_hi:
                pass
            elif outer == 'left' and c < x_lo < e:
                a, cut_a = x_lo - 0.02, True
            elif outer == 'right' and c >= x_hi and a < x_hi:
                e, cut_e = x_hi + 0.02, True
            else:
                continue
            if outer == 'left':
                cut_a = cut_a or a < x_lo - 0.02
                a = max(a, x_lo - 0.02)
            elif outer == 'right':
                cut_e = cut_e or e > x_hi + 0.02
                e = min(e, x_hi + 0.02)
            if skip and skip(c, s0):
                continue
            a += rng.uniform(0.003, 0.012)
            e -= rng.uniform(0.003, 0.012)
            lo = s0 + rng.uniform(-0.012, 0.012)
            top = min(s0 + TILE_L, length + 0.01)
            t = (m @ Matrix.Translation(((a + e) / 2, lo, base + TILE_T)) @ Matrix.Rotation(-tilt, 4, 'X')
                 @ Matrix.Rotation(math.radians(rng.uniform(-2.5, 2.5)), 4, 'Z') @ Matrix.Rotation(math.radians(rng.uniform(-1.5, 1.5)), 4, 'Y'))
            # On a narrow cut-off piece or a short top row the corner cut is smaller, or the cuts would cross.
            h, ch = (e - a) / 2, min(TILE_CHAMFER, 0.4 * (e - a), 0.5 * (top - lo))
            # A slate cut flush with the barge board keeps a square corner on that side.
            outline = [(-h, top - lo), (-h, 0 if cut_a else ch), *([] if cut_a else [(-h + ch, 0)]),
                       *([] if cut_e else [(h - ch, 0)]), (h, 0 if cut_e else ch), (h, top - lo)]
            b.slab(outline, TILE_T, 'GM_Roof', t)
        row += 1
        s0 += TILE_EXPOSE


def ridge_caps(b, frames, x_lo, x_hi, length, pitch, grid=0.0, outer=None, base=ROOF_T):
    """Ridge slates: two plates per cap meeting over the ridge, one cap every TILE_W, each one tipped to lap over the next.
    Ownership and the outer edge work as for the rows of slates."""
    z = base + TILE_T + 0.006
    reach = length + (z + 0.025) * math.tan(pitch) + 0.01
    k = math.floor((x_lo - 0.3 + grid) / TILE_W)
    while True:
        c = k * TILE_W - grid
        k += 1
        if c - 0.15 >= x_hi:
            break
        a, e = c - 0.15, c + 0.15
        if x_lo <= c < x_hi:
            pass
        elif outer == 'left' and c < x_lo < e:
            pass
        elif outer == 'right' and c >= x_hi and a < x_hi:
            pass
        else:
            continue
        if outer == 'left':
            a = max(a, x_lo - 0.03)
        elif outer == 'right':
            e = min(e, x_hi + 0.03)
        lap = Matrix.Translation((c, 0, 0)) @ Matrix.Rotation(math.atan2(0.02, 0.3), 4, 'Y')
        for m in frames:
            b.box((a - c, length - 0.15, z), (e - c, reach, z + 0.025), 'GM_Roof', m @ lap)


def roof_frames(depth):
    h, p = depth / 2, roof_pitch(depth)
    eave = Matrix.Translation((0, -h - EAVE, -EAVE * math.tan(p))) @ Matrix.Rotation(p, 4, 'X')
    return eave, Matrix.Scale(-1, 4, (0, 1, 0)) @ eave


def roof(depth, variant=0, hole=False, edge=None):
    """One cell of the slate roof along the ridge: boards, rows of slates, ridge caps. edge 'left' or 'right' makes the gable
    overhang of that end of the house, with a barge board on its outer side."""
    b = Builder()
    h = depth / 2
    length = (h + EAVE) / math.cos(roof_pitch(depth))
    w = GABLE_OVH / 2 if edge else 0.5
    # x = 0 of a cell is its middle, half a cell off the slate grid; the end overhangs are centred 0.15 out from the house ends.
    grid = {None: 0.5, 'left': -0.15, 'right': 0.15}[edge]
    rng = random.Random(depth * 1000 + variant * 101 + (hole and 7) + {None: 0, 'left': 13, 'right': 29}[edge])
    a, c = length * 0.35, length * 0.7
    for side, m in enumerate(roof_frames(depth)):
        broken = hole and side == 0
        if broken:
            b.box((-w, 0, 0), (w, a, ROOF_T), 'GM_Plank', m)
            b.box((-w, c, 0), (w, length, ROOF_T), 'GM_Plank', m)
            b.box((-w, a, 0), (-0.12, c, ROOF_T), 'GM_Plank', m)
            for x in (-0.05, 0.3):
                b.box((x - 0.05, a, -0.1), (x + 0.05, c, 0), 'GM_Timber', m)
        else:
            b.box((-w, 0, 0), (w, length, ROOF_T), 'GM_Plank', m)
        b.box((-w, -0.06, -0.12), (w, -0.02, ROOF_T), 'GM_Plank', m)

        def skip(x, s, broken=broken):
            if not broken:
                return False
            if a - 0.1 < s < c and x > -0.2:
                return True
            # Slates around the hole are loose: some are gone.
            return a - 0.4 < s < c + 0.3 and x > -0.45 and rng.random() < 0.35

        slate_rows(b, m, -w, w, length, rng, grid, edge, skip=skip)
        if edge == 'left':
            b.box((-w - 0.03, -0.02, -0.16), (-w, length, ROOF_T + 0.04), 'GM_Plank', m)
        elif edge == 'right':
            b.box((w, -0.02, -0.16), (w + 0.03, length, ROOF_T + 0.04), 'GM_Plank', m)
    ridge_caps(b, roof_frames(depth), -w, w, length, roof_pitch(depth), grid, edge)
    return b


def dormer(big, depth):
    """Dormer window on the front slope. Pivot: the line of the front wall at the top of the walls, as for the roof cells.
    The big one is a dovecote with louvres and a landing board, the small one a barred attic window. Its roof runs back into
    the main roof; the part inside is hidden under it."""
    b = Builder()
    wd, hgt, yf = (0.8, 0.75, 0.55) if big else (0.5, 0.45, 0.7)
    tan_p = math.tan(roof_pitch(depth))
    zb = yf * tan_p
    zs = zb + 0.22
    zw = zs + hgt
    hw = wd / 2
    post = 0.09
    # Front frame.
    for x in (-hw, hw - post):
        b.box((x, yf, zb), (x + post, yf + post, zw), 'GM_Timber')
    b.box((-hw, yf, zs - 0.08), (hw, yf + post, zs), 'GM_Timber')
    b.box((-hw, yf, zw - 0.08), (hw, yf + post, zw), 'GM_Timber')
    b.box((-hw, yf + 0.14, zb), (hw, yf + 0.16, zw), 'GM_Lead')
    x0, x1 = -hw + post, hw - post
    if big:
        z = zs + 0.05
        while z < zw - 0.12:
            m = Matrix.Translation((0, yf + 0.04, z)) @ Matrix.Rotation(math.radians(-35), 4, 'X')
            b.box((x0, -0.005, 0), (x1, 0.005, 0.09), 'GM_Plank', m)
            z += 0.08
        b.box((x0 + 0.03, yf - 0.14, zs - 0.03), (x1 - 0.03, yf + 0.02, zs), 'GM_Plank')
    else:
        for i in range(1, 5):
            x = x0 + (x1 - x0) * i / 5
            b.box((x - 0.008, yf + 0.03, zs), (x + 0.008, yf + 0.05, zw - 0.08), 'GM_Lead')
        zm = (zs + zw - 0.08) / 2
        b.box((x0, yf + 0.025, zm - 0.01), (x1, yf + 0.055, zm + 0.01), 'GM_Lead')
    # Cheeks: from the front wall back to where the main roof comes up to the dormer eave.
    yc = (zw - 0.15) / tan_p
    cheek = [(yf, zb), (yf, zw), (yc, zw), (yc, yc * tan_p)]
    b.prism_yz(cheek, -hw, -hw + 0.03, 'GM_Plank')
    b.prism_yz(cheek, hw - 0.03, hw, 'GM_Plank')
    # Gable roof of the dormer, ridge running back into the main roof.
    dp = math.radians(48)
    ovh = 0.1
    ze = zw - 0.06
    run = hw + ovh
    zr = ze + run * math.tan(dp)
    y0 = yf - 0.15
    # The dormer ridge goes into the main roof a little past where it meets it; dormers stand on roofs of depth 4 and more.
    y1 = zr / tan_p + 0.15
    ls = run / math.cos(dp)
    base = 0.05
    m_right = Matrix(((0, -math.cos(dp), math.sin(dp), run), (1, 0, 0, y0), (0, math.sin(dp), math.cos(dp), ze), (0, 0, 0, 1)))
    frames = (m_right, Matrix.Scale(-1, 4, (1, 0, 0)) @ m_right)
    rng = random.Random(77 if big else 78)
    for m in frames:
        b.box((0, 0, 0), (y1 - y0, ls, base), 'GM_Plank', m)
        slate_rows(b, m, 0, y1 - y0, ls, rng, 0.0, 'left', base=base)
        b.box((-0.03, -0.02, -0.1), (0, ls, base + 0.04), 'GM_Plank', m)
    gable_pts = [(-hw, zw), (hw, zw), (0, zr - 0.08)]
    b.prism_xz(gable_pts, yf + 0.02, yf + 0.05, 'GM_Plank')
    ridge_caps(b, frames, 0, y1 - y0, ls, dp, 0.0, 'left', base)
    return b


def thatch_profile(s0, s1, rounded):
    """Section of a thatch slab across the slope: s along the slope, t up from the rafters; the eave end is rounded."""
    t = THATCH_T
    pts = [(s1, 0), (s1, t)]
    if rounded:
        pts += [(s0 + 0.32, t), (s0 + 0.15, t * 0.93), (s0 + 0.05, t * 0.76), (s0, t * 0.5), (s0 + 0.03, t * 0.22), (s0 + 0.12, 0)]
    else:
        pts += [(s0, t), (s0, 0)]
    return pts


def thatch(depth, width=1.0, hole=False):
    """One cell of the thatch roof: thick slabs with a rounded eave and a rolled ridge."""
    b = Builder()
    h, r, p = depth / 2, ROOF_RISE, roof_pitch(depth)
    length = (h + EAVE) / math.cos(p)
    s0 = -THATCH_EAVE
    w = width / 2
    for side, m in enumerate(roof_frames(depth)):
        if hole and side == 0:
            a, c = length * 0.35, length * 0.7
            b.prism_yz(thatch_profile(s0, a, True), -w, w, 'GM_Thatch', m)
            b.prism_yz(thatch_profile(c, length, False), -w, w, 'GM_Thatch', m)
            b.prism_yz(thatch_profile(a, c, False), -w, -0.12, 'GM_Thatch', m)
            for x in (-0.05, 0.3):
                b.box((x - 0.05, a, -0.1), (x + 0.05, c, 0), 'GM_Timber', m)
        else:
            b.prism_yz(thatch_profile(s0, length, True), -w, w, 'GM_Thatch', m)
    top = r + THATCH_T / math.cos(p)
    roll = [(0.24 * math.cos(math.radians(a)), top - 0.08 + 0.2 * math.sin(math.radians(a))) for a in range(0, 360, 30)]
    b.prism_yz(roll, -w, w, 'GM_Thatch')
    return b


def joists_1():
    """Under a jettied upper floor: floor boards and two joist ends sticking out past the upper wall."""
    b = Builder()
    for x in (-0.25, 0.25):
        b.box((x - TIMBER * 0.4, -JETTY - 0.07, -0.17), (x + TIMBER * 0.4, TIMBER, -0.02), 'GM_Timber')
    b.box((-0.5, -JETTY, -0.02), (0.5, 0, 0), 'GM_Plank')
    return b


def jetty_bracket():
    """Curved bracket under a jettied upper floor, on a ground floor post. Pivot as for joists_1: the line of the lower wall at the top
    of the lower floor. Solid timber from the post up to the floor boards, its lower outer edge cut in a quarter ellipse; it reaches
    out under the sill of the upper wall."""
    b = Builder()
    post, top, reach, drop = 0.02, -0.02, 0.47, 0.8
    cy, cz = post - reach, top - drop
    corner = (post, top)
    arc_pts = [(cy + reach * math.cos(math.radians(a)), cz + drop * math.sin(math.radians(a))) for a in range(0, 91, 10)]
    for p, q in zip(arc_pts, arc_pts[1:]):
        b.prism_yz([corner, p, q], -TIMBER * 0.4, TIMBER * 0.4, 'GM_Timber')
    return b


# Lean-to along an end wall of the ground floor: a board roof falling away from the wall onto a beam on posts.
LEAN_WALL_Z = 2.3
LEAN_EDGE_Z = 1.85
LEAN_REACH = 1.6
LEAN_POST_Y = -1.5
LEAN_BEAM_Z = 1.77


def lean_to():
    """One cell of a lean-to. Pivot as for a wall cell of the ground floor (bottom middle of the cell, wall face at Y = 0); the roof
    runs out to -Y. A ledger on the wall, a rafter in the middle of the cell, the eave beam that rests on the posts, boards on top."""
    b = Builder()
    tilt = math.atan2(LEAN_WALL_Z - LEAN_EDGE_Z, LEAN_REACH)
    m = Matrix.Translation((0, 0, LEAN_WALL_Z)) @ Matrix.Rotation(tilt, 4, 'X')
    b.box((-0.505, -LEAN_REACH - 0.05, 0), (0.505, 0, 0.035), 'GM_Plank', m)
    b.box((-0.5, -0.1, LEAN_WALL_Z - 0.14), (0.5, 0, LEAN_WALL_Z - 0.01), 'GM_Timber')
    b.beam((0, -0.05, LEAN_WALL_Z - 0.05), (0, LEAN_POST_Y - 0.05, LEAN_BEAM_Z + 0.08), 0.08, 'GM_Timber')
    b.box((-0.5, LEAN_POST_Y - 0.06, LEAN_BEAM_Z), (0.5, LEAN_POST_Y + 0.06, LEAN_BEAM_Z + 0.1), 'GM_Timber', bevel=0.008)
    return b


def lean_to_post(left, right):
    """Post of a lean-to at a cell boundary, on a flat stone, with knee braces up to the eave beam to -X and/or +X. Pivot as for
    lean_to; the post goes down past the plinth to the ground. Over the post a triangle closes against the wall: a rafter from the
    ledger to the eave beam, a tie from the post top to the wall and a stud up the wall to the ledger."""
    b = Builder()
    b.box((-0.06, LEAN_POST_Y - 0.06, -PLINTH_H), (0.06, LEAN_POST_Y + 0.06, LEAN_BEAM_Z), 'GM_Timber')
    b.beam((0, -0.05, LEAN_WALL_Z - 0.05), (0, LEAN_POST_Y - 0.05, LEAN_BEAM_Z + 0.08), 0.08, 'GM_Timber')
    b.box((-0.05, LEAN_POST_Y + 0.06, LEAN_BEAM_Z - 0.12), (0.05, -0.1, LEAN_BEAM_Z), 'GM_Timber')
    b.box((-0.05, -0.1, LEAN_BEAM_Z - 0.12), (0.05, 0, LEAN_WALL_Z - 0.14), 'GM_Timber')
    b.box((-0.12, LEAN_POST_Y - 0.12, -PLINTH_H - 0.05), (0.12, LEAN_POST_Y + 0.12, -PLINTH_H + 0.06), 'GM_Rock')
    for side, on in ((-1, left), (1, right)):
        if on:
            b.beam((0, LEAN_POST_Y, LEAN_BEAM_Z - 0.4), (side * 0.38, LEAN_POST_Y, LEAN_BEAM_Z + 0.02), 0.07, 'GM_Timber')
    return b


def door_hood():
    """Board hood over a timber door on two brackets. Pivot as for the door cell."""
    b = Builder()
    zh, run, tilt, half = FLOOR_H - TIMBER, 0.6, math.radians(20), DOOR_HALF + 0.3
    m = Matrix.Translation((0, 0, zh)) @ Matrix.Rotation(tilt, 4, 'X')
    b.box((-half, -run, 0), (half, 0, 0.03), 'GM_Plank', m)
    reach = 0.42
    for side in (-1, 1):
        bx = side * (half - 0.1)
        b.prism_yz([(0, zh - 0.45), (-reach, zh - reach * math.tan(tilt)), (0, zh)], bx - 0.035, bx + 0.035, 'GM_Timber')
    return b


def chimney(depth):
    """Stone chimney rising through the back slope: a wide stack up to just above the roof, a sloped shoulder, a narrower shaft,
    a projecting band and a cap slab with a smaller block on top; the flue is an open sooty hole down through the top block."""
    b = Builder()
    r = ROOF_RISE
    top = r + 0.9
    shoulder = r * 0.5 + 0.55
    b.box((-0.38, -0.38, 0), (0.38, 0.38, shoulder), 'GM_Stone')
    b.taper(0, 0, 0.76, 0.56, shoulder, shoulder + 0.22, 'GM_Stone')
    b.box((-0.28, -0.28, shoulder + 0.22), (0.28, 0.28, top - 0.22), 'GM_Stone')
    b.box((-0.34, -0.34, top - 0.22), (0.34, 0.34, top - 0.1), 'GM_Stone')
    b.box((-0.38, -0.38, top - 0.1), (0.38, 0.38, top), 'GM_Stone', bevel=0.015)
    b.box((-0.2, -0.2, top), (0.2, 0.2, top + 0.2), 'GM_Stone', bevel=0.015, hole=(0.11, 0.19, 'GM_Soot'))
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
        'wall_plain_a': wall_plain, 'wall_plain_x': lambda: wall_plain(cross=True),
        'wall_plain_b': lambda: wall_plain(brace=1), 'wall_plain_c': lambda: wall_plain(brace=-1),
        'wall_window': lambda: wall_window(boarded=False),
        'wall_window_boarded': lambda: wall_window(boarded=True),
        'wall_window_closed': wall_window_closed, 'wall_window_ribbon': wall_window_ribbon, 'wall_window_hood': wall_window_hood,
        'wall_window_small': wall_window_small,
        'wall_door': wall_door, 'wall_plank': wall_plank, 'wall_broken': wall_broken,
        'post': post, 'corner_post': corner_post, 'beam_1': beam_1, 'plinth': plinth, 'plinth_corner': plinth_corner,
        'stake': stake, 'rope_1': rope_1, 'scaffold_1': scaffold_1, 'prop': prop, 'joists_1': joists_1, 'jetty_bracket': jetty_bracket,
        'lean_to': lean_to, 'lean_to_post': lambda: lean_to_post(True, True), 'lean_to_post_l': lambda: lean_to_post(True, False),
        'lean_to_post_r': lambda: lean_to_post(False, True), 'door_hood': door_hood,
        'plinth_cellar': plinth_cellar, 'quoin': quoin, 'steps': steps,
        'door_grime': lambda: door_grime(DOOR_HALF, 0.078), 'door_grime_arch': lambda: door_grime(DOOR_HALF, 0.098),
    }
    for prefix, mat in (('stone', 'GM_Stone'), ('render', 'GM_Daub')):
        mods[f'{prefix}_plain'] = lambda mat=mat: ground_plain(mat)
        mods[f'{prefix}_window'] = lambda mat=mat: ground_window(mat)
        mods[f'{prefix}_window_small'] = lambda mat=mat: ground_window_small(mat)
        mods[f'{prefix}_door'] = lambda mat=mat: ground_door(mat)
    for d in DEPTHS:
        mods[f'gable_d{d}'] = lambda d=d: gable(d)
        for v, suffix in enumerate(ROOF_VARIANTS):
            mods[f'roof_d{d}{suffix}'] = lambda d=d, v=v: roof(d, variant=v)
        mods[f'roof_hole_d{d}'] = lambda d=d: roof(d, hole=True)
        mods[f'roof_ovh_d{d}'] = lambda d=d: roof(d, edge='left')
        mods[f'roof_ovhr_d{d}'] = lambda d=d: roof(d, edge='right')
        mods[f'thatch_d{d}'] = lambda d=d: thatch(d)
        mods[f'thatch_hole_d{d}'] = lambda d=d: thatch(d, hole=True)
        mods[f'thatch_ovh_d{d}'] = lambda d=d: thatch(d, width=GABLE_OVH)
        mods[f'chimney_d{d}'] = lambda d=d: chimney(d)
        if d >= 4:
            mods[f'dormer_big_d{d}'] = lambda d=d: dormer(True, d)
            mods[f'dormer_small_d{d}'] = lambda d=d: dormer(False, d)
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


def plain_wall(roll, along, length, rot):
    """Plain timber cell, as the in-engine generator picks it: at the ends of a side a long brace with its top towards the corner, in the
    middle a rail, one time in four with a St Andrew's cross under it."""
    if along < 1 or along > length - 1:
        # Module X runs along the side on the front and right faces and against it on the back and left ones.
        return 'wall_plain_c' if (along < 1) == (rot in (0, 90)) else 'wall_plain_b'
    return 'wall_plain_x' if roll == 0 else 'wall_plain_a'


def window_kind(roll, floor, long_side, masonry):
    """Kind of window for a cell, as the in-engine generator picks it: masonry has the usual and the small window; end walls mostly small
    ones and hoods; the ground floor of a timber house adds closed shutters; upper floors add ribbon windows and hoods."""
    if masonry:
        return 'wall_window_small' if roll < 0.35 else 'wall_window'
    if not long_side:
        return 'wall_window_small' if roll < 0.35 else 'wall_window_hood' if roll < 0.6 else 'wall_window_closed' if roll < 0.7 else 'wall_window'
    if floor == 0:
        return 'wall_window_small' if roll < 0.2 else 'wall_window_closed' if roll < 0.45 else 'wall_window'
    return ('wall_window_ribbon' if roll < 0.25 else 'wall_window_hood' if roll < 0.4 else 'wall_window_closed' if roll < 0.5
            else 'wall_window_small' if roll < 0.65 else 'wall_window')


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
        """Facade cells of the footprint with its front edge at oy: (x, y, rotation, is_front)."""
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

    def lean_to(self, ox, oy, w, d, side):
        """Lean-to along an end wall of the ground floor: a roof cell on every wall cell, posts at the ends and every two cells.
        The end posts carry one knee brace, into the lean-to."""
        for x, y, rot, _ in self.sides(ox, oy, w, d):
            if rot == side:
                self.put('lean_to', x, y, PLINTH_H, rot)
        # The module X axis runs to the back wall on the right end and to the front on the left.
        x = ox if side < 0 else ox + w
        toward_back = 'lean_to_post_r' if side > 0 else 'lean_to_post_l'
        toward_front = 'lean_to_post_l' if side > 0 else 'lean_to_post_r'
        for j in range(0, d + 1, 2):
            self.put(toward_back if j == 0 else toward_front if j == d else 'lean_to_post', x, oy + j, PLINTH_H, side)
        if d % 2:
            self.put(toward_front, x, oy + d, PLINTH_H, side)

    def house(self, ox, oy, w, d, floors, seed, stage='ready', decay=0.0, joint=0, joint_thatch=False, joint_ground=None, door_side=0):
        """joint: for a wing, the end wall it shares with the main house (-90 left, 90 right); the wing takes the main roof cover
        and ground floor and has no jetty, so its back wall stays flush with the main one. door_side: the side with the door
        (rotation; 0 is the front), the door stands in its middle pair of cells."""
        rng = random.Random(seed)
        # Roof cover, jetty and ridge sag come from their own stream, so they do not move the rolls of walls and windows.
        look = random.Random(seed ^ 0x5F3759DF)
        thatch = stage == 'abandoned' or decay > 0 or look.random() < 0.5
        if joint:
            thatch = joint_thatch
        jetty = floors > 1 and d <= 7 and look.random() < (0.0 if joint else 0.7)

        def joined_side(rot):
            return joint != 0 and rot == joint

        def joined_corner(x):
            return joint < 0 and x == ox or joint > 0 and x == ox + w
        sag = look.uniform(0.08, 0.15) if decay > 0 else look.uniform(0.0, 0.06)
        if stage == 'planned':
            for x, y, rot, _ in self.sides(ox, oy, w, d):
                self.put('rope_1', x, y, 0, rot)
            for x, y, _ in list(self.boundaries(ox, oy, w, d)) + list(self.corners(ox, oy, w, d)):
                self.put('stake', x, y)
            return
        built = 1 if stage == 'construction' else floors
        door = rng.randrange(w)
        # Wing, lean-to and door hood: a stream of their own as well.
        mass = random.Random(seed ^ 0x3C6EF372)
        wing_roll = mass.random()
        wing_side = -90 if mass.randrange(2) == 0 else 90
        wing_w = 3
        wing_door = 180 if mass.randrange(2) == 0 else (90 if wing_side < 0 else -90)
        wing_d = max(3, d - 1 - mass.randrange(2))
        lean_roll = mass.random()
        lean_side = -90 if mass.randrange(2) == 0 else 90
        door_hood = mass.random() < 0.5
        if joint or floors < 2 or d < 4 or stage == 'construction' or wing_roll >= 0.4:
            wing_side = 0
        if joint or stage != 'ready' or lean_roll >= 0.3:
            lean_side = 0
        elif lean_side == wing_side:
            lean_side = -lean_side
        # Ground floor kind and the cellar window come from a stream of their own too.
        detail = random.Random(seed ^ 0x6A09E667)
        roll = detail.random()
        ground = 'timber' if stage != 'ready' or decay > 0 or w < 4 or roll < 0.4 else 'render' if roll < 0.75 else 'stone'
        # The door takes two cells, door and door + 1. Door and windows stay in the middle cells: quoins reach into the end cells, and an
        # open shutter would stick out past the corner. A house narrower than 4 has no middle pair: the door goes to the first cells.
        if w >= 4 and (door < 1 or door > w - 3):
            door = 1 + detail.randrange(w - 3)
        elif w < 4:
            door = 0
        if joint:
            ground = joint_ground
        if door_side:
            door = max(0, ((w if door_side == 180 else d) - DOOR_CELLS) // 2)
        # The kind of window on each side of each floor: one more stream.
        panes = random.Random(seed ^ 0x1F83D9AB)
        cellar = -1
        if w > 2 and detail.random() < 0.4:
            cellar = detail.randrange(w - 2)
            cellar += 2 if cellar >= door else 0
        for x, y, rot, front in self.sides(ox, oy, w, d):
            if not joined_side(rot):
                self.put('plinth_cellar' if front and int(x - ox) == cellar else 'plinth', x, y, 0, rot)
        for x, y, rot in self.corners(ox, oy, w, d):
            if not joined_corner(x):
                self.put('plinth_corner', x, y, 0, rot)
        for f in range(built):
            z = PLINTH_H + f * FLOOR_H
            # Upper floors of a jettied house stand half a cell further out at the front and the back: one cell deeper.
            fy, fd = (oy - JETTY, d + 1) if f > 0 and jetty else (oy, d)
            if f == 1 and jetty:
                for x, y, rot, _ in self.sides(ox, oy, w, d):
                    if rot in (0, 180):
                        self.put('joists_1', x, y, z, rot)
                for x, y, rot in self.boundaries(ox, oy, w, d):
                    # No post stands in the middle of the door, so no bracket either.
                    if rot in (0, 180) and not (rot == door_side and x - ox == door + 1):
                        self.put('jetty_bracket', x, y, z, rot)
                for y, rot in ((oy, 0), (oy + d, 180)):
                    for x in (ox + 0.1, ox + w - 0.1):
                        self.put('jetty_bracket', x, y, z, rot)
            masonry = f == 0 and ground != 'timber' and stage != 'construction'
            for x, y, rot in self.corners(ox, fy, w, fd):
                if not joined_corner(x):
                    self.put('quoin' if masonry else 'corner_post', x, y, z, rot)
            if not masonry:
                for x, y, rot in self.boundaries(ox, fy, w, fd):
                    along = x - ox if rot in (0, 180) else y - fy
                    door_middle = f == 0 and stage != 'construction' and rot == door_side and along == door + 1
                    if not joined_side(rot) and not door_middle:
                        self.put('post', x, y, z, rot)
            # One kind of window for the whole side of a floor; no two windows side by side, except ribbon windows, which join into a band.
            kinds = {rot: window_kind(panes.random(), f, rot in (0, 180), masonry) for rot in (0, 180, -90, 90)}
            window_before = {}
            for x, y, rot, front in self.sides(ox, fy, w, fd):
                # The shared end wall of a wing is not built: the main house wall closes it.
                if joined_side(rot):
                    continue
                if stage == 'construction':
                    self.put('beam_1', x, y, z, rot)
                    if rng.random() < 0.35:
                        self.put(rng.choice(('wall_plain_a', 'wall_plain_b')), x, y, z, rot)
                    if front or rot == 90:
                        self.put('scaffold_1', x, y, z, rot)
                    continue
                along, length = (x - ox, w) if rot in (0, 180) else (y - fy, fd)
                is_door = f == 0 and rot == door_side and int(along) == door
                # The second cell of the door is part of the door module.
                if f == 0 and rot == door_side and int(along) == door + 1:
                    window_before[rot] = False
                    continue
                if is_door:
                    name = 'wall_door'
                    # The door module stands on the boundary between its two cells.
                    if rot in (0, 180):
                        x = ox + door + 1
                    else:
                        y = fy + door + 1
                elif rng.random() < (0.35 if front else 0.2) + (0.05 if f > 0 else 0):
                    name = 'wall_window_boarded' if rng.random() < decay * 0.7 else kinds[rot]
                else:
                    name = 'wall_broken' if rng.random() < decay * 0.5 else plain_wall(rng.randrange(4), along, length, rot)
                crowded = window_before.get(rot) and name != 'wall_window_ribbon'
                # A window the wing would cover is a plain wall too.
                covered = rot == wing_side and f < floors - 1 and y - oy > d - wing_d
                if name.startswith('wall_window') and (along < 1 or along > length - 1 or crowded or covered):
                    name = plain_wall(1, along, length, rot)
                window_before[rot] = name.startswith('wall_window')
                if masonry:
                    name = ground + {'wall_door': '_door', 'wall_window': '_window', 'wall_window_small': '_window_small'}.get(name, '_plain')
                self.put(name, x, y, z, rot)
                if is_door:
                    self.put('steps', x, y, 0, rot)
                    self.put('door_grime_arch' if masonry else 'door_grime', x, y, z, rot)
                    if door_hood and not masonry and not jetty and floors > 1:
                        self.put('door_hood', x, y, z, rot)
                if f == 0 and rng.random() < decay * 0.3:
                    self.put('prop', x, y, 0, rot)
        if stage == 'construction':
            return
        if lean_side:
            self.lean_to(ox, oy, w, d, lean_side)
        if wing_side:
            self.house(ox - wing_w if wing_side < 0 else ox + w, oy + d - wing_d, wing_w, wing_d, floors - 1, seed * 7919 + 13, stage,
                       decay, joint=90 if wing_side < 0 else -90, joint_thatch=thatch, joint_ground=ground, door_side=wing_door)
        top = PLINTH_H + floors * FLOOR_H
        rd = d + 1 if jetty else d
        ry = oy - JETTY if jetty else oy
        cover = 'thatch' if thatch else 'roof'
        if joint != -90:
            self.put(f'gable_d{rd}', ox, ry + rd / 2, top, -90)
        if joint != 90:
            self.put(f'gable_d{rd}', ox + w, ry + rd / 2, top, 90)
        tiles = random.Random(seed ^ 0x2545F491)
        dormers, last = 0, -2
        for i in range(w):
            hole = rng.random() < decay * 0.4
            t = (i + 0.5) / w
            cell = f'{cover}_hole_d{rd}' if hole else f'{cover}_d{rd}' + ('' if thatch else tiles.choice(ROOF_VARIANTS))
            self.put(cell, ox + i + 0.5, ry + rd / 2, top - sag * 4 * t * (1 - t))
            if not thatch and not hole and rd >= 4 and 0 < i < w - 1 and i > last + 1 and dormers < 2 and tiles.random() < 0.35:
                dormers, last = dormers + 1, i
                self.put(('dormer_big' if tiles.random() < 0.5 else 'dormer_small') + f'_d{rd}', ox + i + 0.5, ry, top - sag * 4 * t * (1 - t))
        if joint != -90:
            self.put(f'{cover}_ovh_d{rd}', ox - GABLE_OVH / 2, ry + rd / 2, top)
        if joint != 90:
            if thatch:
                self.put(f'thatch_ovh_d{rd}', ox + w + GABLE_OVH / 2, ry + rd / 2, top, 180)
            else:
                self.put(f'roof_ovhr_d{rd}', ox + w + GABLE_OVH / 2, ry + rd / 2, top)
        if rng.random() < 0.8:
            self.put(f'chimney_d{rd}', ox + rng.randrange(w) + 0.5, ry + rd * 0.75, top)


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
    town.house(18.5, 7, 4, 4, 2, seed=38)
    ground(scene, 70, (11, 6))
    setup_render(scene, (11, 6, 1), 70, out)
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
