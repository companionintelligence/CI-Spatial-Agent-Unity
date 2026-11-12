"""Builds the CI Companion avatar and exports it as a GLB.

The avatar is a stylized, Companion Intelligence-owned character generated entirely by this
script: every vertex, bone, weight, material colour and blend shape comes from the code below.
No meshes, textures, fonts, scans or third-party models are imported, so the output carries no
third-party rights (Blender's licence does not extend to what it outputs).

Run headless from the repository root (Blender 5.1; 4.2+ should also work):

    blender --background --factory-startup --python tools/avatar/build_ci_companion.py -- \
        --out Assets/XRScan/Avatar/Models/CI_Companion.glb \
        [--render docs/images/ci_companion_avatar.png] \
        [--visemes docs/images/ci_companion_visemes.png] \
        [--blend <scratch>/ci_companion.blend]

Contract with the Unity side (Assets/XRScan/Avatar):
  * "Face" is the only skinned mesh with blend shapes, so AvatarLoadController picks it as the
    face renderer. It carries the 15 Oculus visemes (viseme_sil ... viseme_U) that
    AvatarLipSync maps uLipSync phonemes onto (A->viseme_aa, I->viseme_I, U->viseme_U,
    E->viseme_E, O->viseme_O, S->viseme_SS), plus mouthOpen / jawOpen (VisemeController's
    amplitude fallback), mouthSmile and eyeBlinkLeft / eyeBlinkRight. All are authored for
    weights 0..100 (Unity) / 0..1 (glTF).
  * The skeleton uses Mixamo-style bone names (Hips, Spine, ..., Head, LeftArm, ...) so a
    Unity Humanoid avatar can be auto-mapped later. No mesh node name contains "head", so
    AvatarLoadController.FindHead() resolves the Head bone.
  * The character faces glTF +Z (Blender -Y) and stands on the origin, about 1.6 m tall.
"""

import argparse
import math
import os
import sys

import bpy
from mathutils import Vector

COPYRIGHT = "Copyright (c) 2026 LifeScope Inc. dba Companion Intelligence. All rights reserved."

# ---------------------------------------------------------------------------------------------
# Palette (sRGB hex -> linear for Principled BSDF / glTF baseColorFactor)
# ---------------------------------------------------------------------------------------------
PALETTE = {
    #  name           sRGB       roughness
    "Skin":         ("#D49A7A", 0.55),
    "Hair":         ("#3B2A22", 0.55),
    "EyeWhite":     ("#F6F4F0", 0.25),
    "IrisOuter":    ("#1F6670", 0.30),
    "IrisInner":    ("#3FA9AC", 0.30),
    "EyeDark":      ("#15171A", 0.35),
    "MouthInside":  ("#4A1E2A", 0.70),
    "Tongue":       ("#C9606E", 0.50),
    "Hoodie":       ("#1F7A8C", 0.85),
    "HoodieTrim":   ("#165E6C", 0.85),
    "Accent":       ("#3CC6A8", 0.60),
    "Pants":        ("#2A2E35", 0.80),
    "ShoeUpper":    ("#F2F2EF", 0.60),
    "ShoeSole":     ("#165E6C", 0.70),
}


def srgb_to_linear(hex_color):
    hex_color = hex_color.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(hex_color[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


# ---------------------------------------------------------------------------------------------
# Proportions (metres, Blender Z-up, character faces -Y, its left side is +X)
# ---------------------------------------------------------------------------------------------
HEAD_C = Vector((0.0, 0.0, 1.462))   # head ellipsoid centre
HEAD_A = 0.098                        # half width (widest)
HEAD_B = 0.104                        # half depth
HEAD_H = 0.121                        # half height
JAW_TAPER = 0.16                      # x narrowing towards the chin

EYE_X = 0.039
EYE_Z = HEAD_C.z - 0.010
NOSE_Z = HEAD_C.z - 0.036
MOUTH_Z = HEAD_C.z - 0.064

SHOULDER = Vector((0.185, 0.0, 1.288))
ELBOW = Vector((0.272, 0.008, 1.040))
WRIST = Vector((0.312, -0.030, 0.800))
HAND_TIP = Vector((0.322, -0.040, 0.712))
HIP = Vector((0.083, 0.0, 0.860))
KNEE = Vector((0.087, -0.006, 0.475))
ANKLE = Vector((0.087, 0.004, 0.098))
BALL = Vector((0.087, -0.095, 0.030))
TOE = Vector((0.087, -0.160, 0.030))


def jaw_half_width(z):
    t = min(max((HEAD_C.z - z) / HEAD_H, 0.0), 1.0)
    return HEAD_A * (1.0 - JAW_TAPER * t ** 1.4)


def head_surface(x, z):
    """Front (-Y) surface point of the tapered head ellipsoid at (x, z)."""
    ax = jaw_half_width(z)
    q = 1.0 - (x / ax) ** 2 - ((z - HEAD_C.z) / HEAD_H) ** 2
    y = -HEAD_B * math.sqrt(max(q, 0.0))
    return Vector((x, HEAD_C.y + y, z))


def head_point(x, z, offset):
    """Point on the face surface at (x, z), pushed `offset` metres out along the surface normal."""
    h = 0.0004
    p = head_surface(x, z)
    dx = head_surface(x + h, z) - head_surface(x - h, z)
    dz = head_surface(x, z + h) - head_surface(x, z - h)
    n = dz.cross(dx)
    if n.length < 1e-12:
        n = Vector((0.0, -1.0, 0.0))
    n.normalize()
    if n.y > 0.0:
        n = -n
    return p + n * offset


# ---------------------------------------------------------------------------------------------
# Mesh accumulation
# ---------------------------------------------------------------------------------------------
class MeshBuilder:
    def __init__(self, name):
        self.name = name
        self.verts = []
        self.faces = []
        self.face_mats = []
        self.weights = []          # per vertex: {bone: weight}
        self.materials = []        # names, index = material slot
        self.ranges = {}           # tag -> (start, end) vertex index range

    def mat(self, name):
        if name not in self.materials:
            self.materials.append(name)
        return self.materials.index(name)

    def add_verts(self, points, weights):
        start = len(self.verts)
        for i, p in enumerate(points):
            self.verts.append(Vector(p))
            w = weights(p) if callable(weights) else weights
            self.weights.append(dict(w))
        return start

    def add_face(self, idx, material, ref=None):
        """Adds a polygon; if `ref` (an interior point, or a callable centroid->interior point)
        is given the winding is flipped where needed so the face points away from it."""
        idx = list(idx)
        if ref is not None:
            pts = [self.verts[i] for i in idx]
            c = sum(pts, Vector()) / len(pts)
            n = (pts[1] - pts[0]).cross(pts[2] - pts[0])
            if len(pts) == 4:
                n += (pts[2] - pts[0]).cross(pts[3] - pts[0])
            inside = ref(c) if callable(ref) else ref
            if n.length > 1e-14 and n.dot(c - inside) < 0.0:
                idx.reverse()
        self.faces.append(tuple(idx))
        self.face_mats.append(self.mat(material))

    def tag(self, name, start):
        self.ranges[name] = (start, len(self.verts))


def ring_surface(mb, rings, material, weights, ref, start_pole=None, end_pole=None, closed=True):
    """Quads between consecutive rings (lists of equal length) plus optional pole fans."""
    seg = len(rings[0])
    base = mb.add_verts([p for ring in rings for p in ring], weights)
    wrap = seg if closed else seg - 1
    for r in range(len(rings) - 1):
        for s in range(wrap):
            a = base + r * seg + s
            b = base + r * seg + (s + 1) % seg
            c = base + (r + 1) * seg + (s + 1) % seg
            d = base + (r + 1) * seg + s
            mb.add_face((a, b, c, d), material, ref)
    for pole, ring_index in ((start_pole, 0), (end_pole, len(rings) - 1)):
        if pole is None:
            continue
        p = mb.add_verts([pole], weights)
        for s in range(wrap):
            a = base + ring_index * seg + s
            b = base + ring_index * seg + (s + 1) % seg
            mb.add_face((a, b, p), material, ref)
    return base


def basis_for(axis):
    axis = axis.normalized()
    helper = Vector((0.0, 0.0, 1.0)) if abs(axis.z) < 0.9 else Vector((1.0, 0.0, 0.0))
    u = axis.cross(helper).normalized()
    v = axis.cross(u).normalized()
    return u, v


def capsule(mb, p0, p1, r0, r1, material, weights, segs=16, cap=4, mid=4, squash=1.0):
    """Tapered capsule from p0 to p1. `squash` scales the cross-section along the basis v."""
    p0, p1 = Vector(p0), Vector(p1)
    axis = (p1 - p0)
    u, v = basis_for(axis)
    a = axis.normalized()
    rings = []

    def ring(center, radius):
        return [center + (u * math.cos(t) + v * math.sin(t) * squash) * radius
                for t in (2.0 * math.pi * s / segs for s in range(segs))]

    for k in range(1, cap + 1):          # cap at p0: from near-pole to equator
        th = -math.pi / 2 + math.pi / 2 * k / cap
        rings.append(ring(p0 + a * r0 * math.sin(th), r0 * math.cos(th)))
    for k in range(1, mid):
        t = k / mid
        rings.append(ring(p0.lerp(p1, t), r0 + (r1 - r0) * t))
    for k in range(0, cap):              # cap at p1: equator to near-pole
        th = math.pi / 2 * k / cap
        rings.append(ring(p1 + a * r1 * math.sin(th), r1 * math.cos(th)))

    def ref(c):
        t = max(0.0, min(1.0, (c - p0).dot(a) / max(axis.length, 1e-9)))
        return p0.lerp(p1, t)

    return ring_surface(mb, rings, material, weights, ref, p0 - a * r0, p1 + a * r1)


def ellipsoid(mb, center, radii, material, weights, segs=24, rings=14, deform=None):
    center = Vector(center)
    rs = []
    for k in range(1, rings):
        el = -math.pi / 2 + math.pi * k / rings
        ring = []
        for s in range(segs):
            az = 2.0 * math.pi * s / segs
            d = Vector((math.cos(el) * math.sin(az), -math.cos(el) * math.cos(az), math.sin(el)))
            p = Vector((d.x * radii[0], d.y * radii[1], d.z * radii[2]))
            if deform:
                p = deform(p, d)
            ring.append(center + p)
        rs.append(ring)
    bottom = center + Vector((0, 0, -radii[2]))
    top = center + Vector((0, 0, radii[2]))
    if deform:
        bottom = center + deform(Vector((0, 0, -radii[2])), Vector((0, 0, -1)))
        top = center + deform(Vector((0, 0, radii[2])), Vector((0, 0, 1)))
    return ring_surface(mb, rs, material, weights, center, bottom, top)


def superellipsoid(mb, center, half, e_lat, e_lon, material, weights, segs=24, rings=12):
    center = Vector(center)

    def sc(w, m):
        c, s = math.cos(w), math.sin(w)
        return (math.copysign(abs(c) ** m, c), math.copysign(abs(s) ** m, s))

    rs = []
    for k in range(1, rings):
        eta = -math.pi / 2 + math.pi * k / rings
        ce, se = sc(eta, e_lat)
        ring = []
        for s in range(segs):
            om = 2.0 * math.pi * s / segs
            co, so = sc(om, e_lon)
            ring.append(center + Vector((half[0] * ce * so, -half[1] * ce * co, half[2] * se)))
        rs.append(ring)
    return ring_surface(mb, rs, material, weights, center,
                        center + Vector((0, 0, -half[2])), center + Vector((0, 0, half[2])))


def torus(mb, center, major, minor, material, weights, segs=36, csegs=10, scale_y=1.0, tilt_x=0.0):
    center = Vector(center)
    ct, st = math.cos(tilt_x), math.sin(tilt_x)

    def xf(p):  # scale y, then tilt about X (front edge down for positive tilt)
        p = Vector((p.x, p.y * scale_y, p.z))
        return Vector((p.x, p.y * ct - p.z * st, p.y * st + p.z * ct)) + center

    rings = []
    for s in range(segs):
        az = 2.0 * math.pi * s / segs
        core = Vector((math.sin(az) * major, -math.cos(az) * major, 0.0))
        out = Vector((math.sin(az), -math.cos(az), 0.0))
        rings.append([xf(core + (out * math.cos(t) + Vector((0, 0, 1)) * math.sin(t)) * minor)
                      for t in (2.0 * math.pi * c / csegs for c in range(csegs))])
    rings.append(rings[0])
    base = mb.add_verts([p for r in rings[:-1] for p in r], weights)
    for s in range(segs):
        for c in range(csegs):
            a = base + s * csegs + c
            b = base + s * csegs + (c + 1) % csegs
            s2 = (s + 1) % segs
            cc = base + s2 * csegs + (c + 1) % csegs
            d = base + s2 * csegs + c

            def ref(cen, s=s):
                # nearest point on the (scaled, tilted) core circle
                local = cen - center
                local = Vector((local.x, local.y * ct + local.z * st, -local.y * st + local.z * ct))
                local = Vector((local.x, local.y / scale_y, 0.0))
                if local.length < 1e-9:
                    local = Vector((0.0, -1.0, 0.0))
                return xf(local.normalized() * major)

            mb.add_face((a, b, cc, d), material, ref)
    return base


def lathe(mb, profile, ky, material, weights, segs=40, close_bottom=True, close_top=True):
    """Revolves (z, rx) profile pairs about Z; depth radius = rx * ky."""
    rings = []
    for z, r in profile:
        rings.append([Vector((math.sin(a) * r, -math.cos(a) * r * ky, z))
                      for a in (2.0 * math.pi * s / segs for s in range(segs))])

    def ref(c):
        return Vector((0.0, 0.0, c.z))

    bottom = Vector((0, 0, profile[0][0])) if close_bottom else None
    top = Vector((0, 0, profile[-1][0])) if close_top else None
    return ring_surface(mb, rings, material, weights, ref, bottom, top)


def grid_faces(mb, base, cols, rows, material):
    """Faces for a (cols x rows) vertex grid laid out column-major in (x, z) parameter space.
    Winding (i,j)->(i+1,j)->(i+1,j+1)->(i,j+1) faces -Y, i.e. out of the face."""
    for i in range(cols - 1):
        for j in range(rows - 1):
            a = base + i * rows + j
            b = base + (i + 1) * rows + j
            c = base + (i + 1) * rows + j + 1
            d = base + i * rows + j + 1
            mb.add_face((a, b, c, d), material)


def disc_faces(mb, base, rings, segs, material):
    """Faces for a polar decal: vertex 0 = centre, then `rings` rings of `segs` (CCW seen from
    the front, i.e. from -Y looking +Y with +X to the right)."""
    for s in range(segs):
        a = base + 1 + s
        b = base + 1 + (s + 1) % segs
        mb.add_face((base, a, b), material)
    for r in range(rings - 1):
        for s in range(segs):
            a = base + 1 + r * segs + s
            b = base + 1 + r * segs + (s + 1) % segs
            c = base + 1 + (r + 1) * segs + (s + 1) % segs
            d = base + 1 + (r + 1) * segs + s
            mb.add_face((a, b, c, d), material)


# ---------------------------------------------------------------------------------------------
# Face decals (parametric so blend shapes can be generated from the same functions)
# ---------------------------------------------------------------------------------------------
MOUTH_COLS = 27
MOUTH_ROWS = 7
TEETH_ROWS = 3
TONGUE_ROWS = 4

MOUTH_BASE = dict(w=0.0215, hu=0.0011, hl=0.0011, p=0.75, s=0.0016, dz=0.0,
                  teeth_u=0.0, teeth_l=0.0, tongue=0.0, tongue_w=0.6)

# Viseme / expression parameters (deviations from MOUTH_BASE).
MOUTH_SHAPES = {
    "mouthOpen":  dict(hu=0.0050, hl=0.0115, w=0.0200, p=0.60, dz=-0.0015, teeth_u=0.22, tongue=0.30),
    "viseme_sil": dict(hu=0.0008, hl=0.0008, w=0.0210),
    "viseme_PP":  dict(hu=0.0005, hl=0.0005, w=0.0195, s=0.0006),
    "viseme_FF":  dict(hu=0.0030, hl=0.0012, w=0.0205, dz=-0.0008, teeth_u=0.95),
    "viseme_TH":  dict(hu=0.0030, hl=0.0034, w=0.0195, teeth_u=0.35, tongue=0.62, tongue_w=0.45),
    "viseme_DD":  dict(hu=0.0032, hl=0.0042, w=0.0205, teeth_u=0.45, teeth_l=0.18, tongue=0.35),
    "viseme_kk":  dict(hu=0.0034, hl=0.0055, w=0.0215, teeth_u=0.35, tongue=0.40),
    "viseme_CH":  dict(hu=0.0034, hl=0.0040, w=0.0160, p=0.55, s=0.0004, teeth_u=0.42, teeth_l=0.30),
    "viseme_SS":  dict(hu=0.0024, hl=0.0024, w=0.0240, s=0.0022, teeth_u=0.55, teeth_l=0.45),
    "viseme_nn":  dict(hu=0.0026, hl=0.0036, w=0.0205, teeth_u=0.40, tongue=0.45),
    "viseme_RR":  dict(hu=0.0032, hl=0.0042, w=0.0170, p=0.55, teeth_u=0.35, tongue=0.30),
    "viseme_aa":  dict(hu=0.0062, hl=0.0145, w=0.0205, p=0.55, dz=-0.0020, teeth_u=0.20, tongue=0.33),
    "viseme_E":   dict(hu=0.0042, hl=0.0075, w=0.0250, p=0.70, s=0.0020, teeth_u=0.32, tongue=0.25),
    "viseme_I":   dict(hu=0.0030, hl=0.0042, w=0.0265, s=0.0028, teeth_u=0.55, teeth_l=0.35),
    "viseme_O":   dict(hu=0.0072, hl=0.0105, w=0.0130, p=0.50, dz=-0.0010, teeth_u=0.12, tongue=0.22),
    "viseme_U":   dict(hu=0.0040, hl=0.0050, w=0.0088, p=0.50, s=0.0, tongue=0.0),
    "mouthSmile": dict(hu=0.0012, hl=0.0020, w=0.0260, s=0.0072),
    "jawOpen":    dict(hu=0.0030, hl=0.0140, w=0.0195, p=0.60, dz=-0.0025, teeth_u=0.20, tongue=0.30),
}
# glTF morph target order; the first 17 follow the Ready Player Me / Oculus ordering so any
# tooling that assumed that layout keeps working.
SHAPE_ORDER = ["mouthOpen", "viseme_sil", "viseme_PP", "viseme_FF", "viseme_TH", "viseme_DD",
               "viseme_kk", "viseme_CH", "viseme_SS", "viseme_nn", "viseme_RR", "viseme_aa",
               "viseme_E", "viseme_I", "viseme_O", "viseme_U", "mouthSmile",
               "eyeBlinkLeft", "eyeBlinkRight", "jawOpen"]


def mouth_layers(params):
    """Returns {layer: [points]} for the mouth decal layers, column-major grids."""
    m = dict(MOUTH_BASE)
    m.update(params)
    w, hu, hl, p, s, dz = m["w"], m["hu"], m["hl"], m["p"], m["s"], m["dz"]

    def centre(t):
        return MOUTH_Z + dz + s * (t * t - 0.35)

    def shape(t):
        return max(0.0, 1.0 - t * t) ** p

    def upper(t):
        return centre(t) + hu * shape(t)

    def lower(t):
        return centre(t) - hl * shape(t)

    out = {"interior": [], "tongue": [], "teeth_u": [], "teeth_l": []}
    ts = [-1.0 + 2.0 * i / (MOUTH_COLS - 1) for i in range(MOUTH_COLS)]
    for t in ts:
        lo, up = lower(t), upper(t)
        for j in range(MOUTH_ROWS):
            out["interior"].append(head_point(w * t, lo + (up - lo) * j / (MOUTH_ROWS - 1), 0.0006))
    tw = m["tongue_w"]
    for t in ts:
        tt = t * tw
        lo, up = lower(tt), upper(tt)
        top = lo + m["tongue"] * (up - lo) * math.sqrt(max(0.0, 1.0 - t * t))
        for j in range(TONGUE_ROWS):
            out["tongue"].append(head_point(w * tt, lo + 0.00015 + (top - lo) * j / (TONGUE_ROWS - 1), 0.0008))
    for t in ts:
        tt = t * 0.78
        lo, up = lower(tt), upper(tt)
        bottom = max(lo, up - m["teeth_u"] * (up - lo))
        for j in range(TEETH_ROWS):
            out["teeth_u"].append(head_point(w * tt, bottom + (up - 0.0001 - bottom) * j / (TEETH_ROWS - 1), 0.0010))
    for t in ts:
        tt = t * 0.62
        lo, up = lower(tt), upper(tt)
        top = min(up, lo + m["teeth_l"] * (up - lo))
        for j in range(TEETH_ROWS):
            out["teeth_l"].append(head_point(w * tt, lo + 0.0001 + (top - lo - 0.0001) * j / (TEETH_ROWS - 1), 0.0010))
    return out


EYE_RINGS = 4
EYE_SEGS = 28
LASH_COLS = 21


def eye_layers(side, blink):
    """Decal layers for one eye. side=+1 for the character's left (+X). blink 0..1."""
    cx, cz = side * EYE_X, EYE_Z
    sx, sz = 0.0148, 0.0168
    close_v = -0.28 * sz
    k = 1.0 - 0.94 * blink

    def place(dx, dz, off):
        dz = close_v + (dz - close_v) * k
        return head_point(cx + dx, cz + dz, off)

    def disc(rx, rz, ox, oz, off):
        pts = [place(ox, oz, off)]
        for r in range(1, EYE_RINGS + 1):
            f = r / EYE_RINGS
            for s in range(EYE_SEGS):
                a = 2.0 * math.pi * s / EYE_SEGS
                pts.append(place(ox + math.cos(a) * rx * f, oz + math.sin(a) * rz * f, off))
        return pts

    out = {
        "sclera": disc(sx, sz, 0.0, 0.0, 0.0005),
        "iris_outer": disc(0.0108, 0.0112, 0.0, -0.0010, 0.0007),
        "iris_inner": disc(0.0080, 0.0083, 0.0, -0.0012, 0.0008),
        "pupil": disc(0.0052, 0.0054, 0.0, -0.0012, 0.0009),
        "highlight": disc(0.0026, 0.0026, -0.0038, 0.0034, 0.0010),
    }
    # Upper lash line: follows the top of the sclera while open, a soft curve when closed.
    lash = []
    for i in range(LASH_COLS):
        u = -1.0 + 2.0 * i / (LASH_COLS - 1)          # -1 inner corner .. +1 outer corner
        x = u * sx * 1.08
        open_z = sz * math.sqrt(max(0.0, 1.0 - (u * 0.97) ** 2))
        closed_z = close_v - 0.0022 * (1.0 - u * u)
        z = open_z + (closed_z - open_z) * blink
        thick = 0.0024 * (1.0 - 0.55 * abs(u)) + (0.0006 if u > 0.6 else 0.0)
        wing = 0.0018 * max(0.0, u - 0.75) / 0.25      # small flick at the outer corner
        for j in range(2):
            zz = z - thick * 0.35 + thick * j + wing * j
            lash.append(head_point(cx + side * x, cz + zz, 0.0012))
    out["lash"] = lash
    return out


EYE_LAYER_MATERIALS = [("sclera", "EyeWhite"), ("iris_outer", "IrisOuter"), ("iris_inner", "IrisInner"),
                       ("pupil", "EyeDark"), ("highlight", "EyeWhite"), ("lash", "EyeDark")]
MOUTH_LAYER_MATERIALS = [("interior", "MouthInside", MOUTH_ROWS), ("tongue", "Tongue", TONGUE_ROWS),
                         ("teeth_u", "EyeWhite", TEETH_ROWS), ("teeth_l", "EyeWhite", TEETH_ROWS)]


def brow_points(side):
    pts = []
    cols = 18
    x0, x1 = 0.022, 0.058
    for i in range(cols):
        u = i / (cols - 1)
        x = x0 + (x1 - x0) * u
        z = EYE_Z + 0.0285 + 0.0045 * math.sin(math.pi * min(1.0, u * 1.15)) - 0.004 * u
        thick = 0.0046 * (1.0 - u) + 0.0016 * u
        for j in range(2):
            pts.append(head_point(side * x, z - thick * 0.5 + thick * j, 0.0006))
    return pts, cols


# ---------------------------------------------------------------------------------------------
# Build
# ---------------------------------------------------------------------------------------------
def build_face(mb):
    head_w = {"Head": 1.0}

    # Head skin: tapered ellipsoid.
    def head_deform(p, d):
        z = HEAD_C.z + d.z * HEAD_H
        return Vector((d.x * jaw_half_width(z), d.y * HEAD_B, d.z * HEAD_H))

    ellipsoid(mb, HEAD_C, (HEAD_A, HEAD_B, HEAD_H), "Skin", head_w, segs=56, rings=36, deform=head_deform)

    # Nose and ears (partially embedded closed shapes).
    nose = head_surface(0.0, NOSE_Z) + Vector((0.0, 0.0032, 0.0))
    ellipsoid(mb, nose, (0.0105, 0.0095, 0.0125), "Skin", head_w, segs=18, rings=12)
    for side in (1, -1):
        ear = Vector((side * (jaw_half_width(HEAD_C.z - 0.01) - 0.004), 0.006, HEAD_C.z - 0.012))
        ellipsoid(mb, ear, (0.012, 0.021, 0.029), "Skin", head_w, segs=16, rings=10)

    # Hair: an outer shell whose lower boundary follows a hairline, plus a rim tucking it in.
    build_hair(mb, head_w)

    # Brows.
    for side in (1, -1):
        pts, cols = brow_points(side)
        start = mb.add_verts(pts, head_w)
        if side > 0:
            grid_faces(mb, start, cols, 2, "Hair")
        else:  # mirrored columns run right-to-left: flip winding
            for i in range(cols - 1):
                a = start + i * 2
                mb.add_face((a + 2, a, a + 1, a + 3), "Hair")

    # Eyes (decal layers; tagged for the blink shapes).
    for side, tag in ((1, "eye_L"), (-1, "eye_R")):
        layers = eye_layers(side, 0.0)
        start = len(mb.verts)
        for name, mat in EYE_LAYER_MATERIALS:
            pts = layers[name]
            base = mb.add_verts(pts, head_w)
            if name == "lash":
                if side > 0:
                    grid_faces(mb, base, LASH_COLS, 2, mat)
                else:
                    for i in range(LASH_COLS - 1):
                        a = base + i * 2
                        mb.add_face((a + 2, a, a + 1, a + 3), mat)
            else:
                disc_faces(mb, base, EYE_RINGS, EYE_SEGS, mat)
        mb.tag(tag, start)

    # Mouth (decal layers; tagged for the mouth shapes).
    layers = mouth_layers({})
    start = len(mb.verts)
    for name, mat, rows in MOUTH_LAYER_MATERIALS:
        base = mb.add_verts(layers[name], head_w)
        grid_faces(mb, base, MOUTH_COLS, rows, mat)
    mb.tag("mouth", start)


def hairline(az):
    """Elevation (radians) of the hairline at azimuth az (0 = front, +pi/2 = character's left)."""
    c = math.cos(az)
    front, side, back = 0.40, 0.12, -0.66
    h = side + (front - side) * c ** 1.6 if c >= 0.0 else side + (back - side) * (-c) ** 1.1
    # side-swept fringe dipping over the character's right brow, and a parting on the left
    h -= 0.20 * math.exp(-((az + 0.30) / 0.42) ** 2)
    h += 0.06 * math.exp(-((az - 0.42) / 0.12) ** 2)
    return h


def build_hair(mb, weights):
    segs, rows = 72, 20

    def shell(az, el, scale):
        d = Vector((math.cos(el) * math.sin(az), -math.cos(el) * math.cos(az), math.sin(el)))
        z = HEAD_C.z + d.z * HEAD_H
        lift = 1.0 + 0.045 * max(0.0, d.z) ** 2                       # volume on top
        quiff = 0.10 * math.exp(-((az - 0.25) / 0.6) ** 2) * math.exp(-((el - 0.95) / 0.35) ** 2)
        k = scale * (lift + quiff)
        return Vector((d.x * jaw_half_width(z) * k, d.y * HEAD_B * k, d.z * HEAD_H * k)) + HEAD_C + Vector((0, 0.003, 0.004))

    outer, inner = [], []
    for r in range(rows):
        f = r / (rows - 1)                     # 0 = near crown, 1 = hairline
        ring_o, ring_i = [], []
        for s in range(segs):
            az = -math.pi + 2.0 * math.pi * s / segs
            el = (math.pi / 2 - 0.02) + (hairline(az) - (math.pi / 2 - 0.02)) * f
            ring_o.append(shell(az, el, 1.075))
            if r == rows - 1:
                ring_i.append(shell(az, el, 0.975))
        outer.append(ring_o)
        if ring_i:
            inner = ring_i
    crown = HEAD_C + Vector((0, 0.003, 0.004 + HEAD_H * 1.075 * 1.045))
    base = ring_surface(mb, outer, "Hair", weights, HEAD_C, start_pole=crown)
    # Rim: tucks the shell's lower edge into the head so it reads as a thick hair mass. Its faces
    # should point away from the crown (down / outwards), so orient against a point above the head.
    last = base + (rows - 1) * segs
    rim = mb.add_verts(inner, weights)
    above = HEAD_C + Vector((0.0, 0.0, HEAD_H * 0.9))
    for s in range(segs):
        a, b = last + s, last + (s + 1) % segs
        c, d = rim + (s + 1) % segs, rim + s
        mb.add_face((a, b, c, d), "Hair", above)


def build_body(mb):
    def spine_weights(p):
        joints = [("Hips", 0.90), ("Spine", 1.01), ("Spine1", 1.12), ("Spine2", 1.24)]
        z = p.z if hasattr(p, "z") else p[2]
        if z <= joints[0][1]:
            return {"Hips": 1.0}
        if z >= joints[-1][1]:
            return {"Spine2": 1.0}
        for (b0, z0), (b1, z1) in zip(joints, joints[1:]):
            if z0 <= z <= z1:
                t = (z - z0) / (z1 - z0)
                return {b0: 1.0 - t, b1: t}
        return {"Spine2": 1.0}

    # Hoodie torso.
    profile = [(0.835, 0.148), (0.86, 0.156), (0.92, 0.158), (1.00, 0.157), (1.08, 0.161),
               (1.15, 0.168), (1.21, 0.170), (1.255, 0.163), (1.285, 0.148), (1.308, 0.118),
               (1.322, 0.082), (1.330, 0.050)]
    lathe(mb, profile, 0.64, "Hoodie", spine_weights, segs=44)
    torus(mb, (0, 0, 0.848), 0.150, 0.014, "HoodieTrim", spine_weights, segs=44, csegs=10, scale_y=0.66)
    # Kangaroo pocket.
    superellipsoid(mb, (0.0, -0.088, 0.945), (0.088, 0.016, 0.050), 0.25, 0.25, "HoodieTrim", spine_weights, segs=28, rings=12)
    # Collar, hood and drawstrings.
    torus(mb, (0, 0.006, 1.318), 0.064, 0.020, "Hoodie", {"Spine2": 1.0}, segs=36, csegs=12, scale_y=0.92, tilt_x=-0.30)
    ellipsoid(mb, (0.0, 0.082, 1.292), (0.112, 0.048, 0.078), "Hoodie", {"Spine2": 1.0}, segs=24, rings=12)
    for side in (1, -1):
        top = Vector((side * 0.026, -0.072, 1.296))
        bottom = Vector((side * 0.031, -0.104, 1.172))
        capsule(mb, top, bottom, 0.0034, 0.0034, "Accent", {"Spine2": 1.0}, segs=8, cap=2, mid=3)
        capsule(mb, bottom + Vector((0, -0.001, -0.004)), bottom + Vector((0, -0.002, -0.020)), 0.0048, 0.0048,
                "ShoeUpper", {"Spine2": 1.0}, segs=8, cap=2, mid=2)
    # Chest badge: a small accent ring with a dot (generic, not a logo).
    torus(mb, (0.060, -0.103, 1.205), 0.012, 0.0022, "Accent", {"Spine2": 1.0}, segs=24, csegs=6, tilt_x=math.radians(84))
    ellipsoid(mb, (0.060, -0.1035, 1.205), (0.0045, 0.0018, 0.0045), "Accent", {"Spine2": 1.0}, segs=12, rings=8)

    # Neck.
    capsule(mb, (0, 0.004, 1.295), (0, 0.006, 1.395), 0.041, 0.039, "Skin", {"Neck": 1.0}, segs=16, cap=3, mid=3)

    # Arms (mirrored).
    for side, pre in ((1, "Left"), (-1, "Right")):
        def m(v):
            return Vector((side * v.x, v.y, v.z))
        sh, el, wr, tip = m(SHOULDER), m(ELBOW), m(WRIST), m(HAND_TIP)
        ellipsoid(mb, sh + Vector((side * -0.008, 0, 0.004)), (0.060, 0.056, 0.058), "Hoodie", {pre + "Arm": 1.0}, segs=20, rings=12)
        capsule(mb, sh, el, 0.053, 0.046, "Hoodie", {pre + "Arm": 1.0}, segs=18, cap=3, mid=4)
        capsule(mb, el, wr, 0.046, 0.041, "Hoodie", {pre + "ForeArm": 1.0}, segs=18, cap=3, mid=4)
        cuff_dir = (wr - el).normalized()
        capsule(mb, wr - cuff_dir * 0.012, wr + cuff_dir * 0.006, 0.043, 0.043, "HoodieTrim", {pre + "ForeArm": 1.0}, segs=18, cap=2, mid=2)
        # Mitten hand + thumb.
        palm_c = wr.lerp(tip, 0.48)
        capsule(mb, wr + cuff_dir * 0.004, tip, 0.024, 0.021, "Skin", {pre + "Hand": 1.0}, segs=14, cap=4, mid=3, squash=0.62)
        thumb0 = wr.lerp(tip, 0.25) + Vector((side * -0.006, -0.018, 0.0))
        thumb1 = palm_c + Vector((side * -0.004, -0.030, -0.012))
        capsule(mb, thumb0, thumb1, 0.0105, 0.0090, "Skin", {pre + "Hand": 1.0}, segs=10, cap=3, mid=2)

    # Pants: a seat/crotch piece that stays mostly under the hoodie hem and between the thighs.
    ellipsoid(mb, (0, 0.006, 0.868), (0.112, 0.088, 0.080), "Pants", {"Hips": 1.0}, segs=28, rings=14)
    for side, pre in ((1, "Left"), (-1, "Right")):
        def m(v):
            return Vector((side * v.x, v.y, v.z))
        hip, knee, ankle, ball, toe = m(HIP), m(KNEE), m(ANKLE), m(BALL), m(TOE)
        capsule(mb, hip, knee, 0.068, 0.054, "Pants", {pre + "UpLeg": 1.0}, segs=18, cap=3, mid=5)
        capsule(mb, knee, ankle + Vector((0, 0, 0.03)), 0.054, 0.046, "Pants", {pre + "Leg": 1.0}, segs=18, cap=3, mid=5)
        # Sneaker: upper + sole.
        shoe_c = Vector((side * 0.087, -0.036, 0.052))
        superellipsoid(mb, shoe_c, (0.050, 0.118, 0.046), 0.42, 0.42, "ShoeUpper", {pre + "Foot": 1.0}, segs=28, rings=14)
        superellipsoid(mb, Vector((side * 0.087, -0.038, 0.012)), (0.054, 0.124, 0.013), 0.25, 0.40, "ShoeSole",
                       {pre + "Foot": 1.0}, segs=28, rings=10)
        capsule(mb, Vector((side * 0.087, -0.004, 0.078)), Vector((side * 0.087, -0.064, 0.090)), 0.006, 0.006,
                "ShoeSole", {pre + "Foot": 1.0}, segs=8, cap=2, mid=2)


BONES = [
    # name, head, tail, parent, roll_axis
    ("Hips", (0, 0, 0.880), (0, 0, 0.960), None, (0, -1, 0)),
    ("Spine", (0, 0, 0.960), (0, 0, 1.060), "Hips", (0, -1, 0)),
    ("Spine1", (0, 0, 1.060), (0, 0, 1.170), "Spine", (0, -1, 0)),
    ("Spine2", (0, 0, 1.170), (0, 0, 1.300), "Spine1", (0, -1, 0)),
    ("Neck", (0, 0.004, 1.300), (0, 0.006, 1.380), "Spine2", (0, -1, 0)),
    ("Head", (0, 0.006, 1.380), (0, 0.006, 1.560), "Neck", (0, -1, 0)),
    ("HeadTop_End", (0, 0.006, 1.560), (0, 0.006, 1.620), "Head", (0, -1, 0)),
]


def bone_list():
    bones = list(BONES)
    for side, pre in ((1, "Left"), (-1, "Right")):
        def m(v):
            return (side * v[0], v[1], v[2])
        bones += [
            (pre + "Shoulder", m((0.035, 0.0, 1.270)), m(SHOULDER), "Spine2", (0, -1, 0)),
            (pre + "Arm", m(SHOULDER), m(ELBOW), pre + "Shoulder", (0, -1, 0)),
            (pre + "ForeArm", m(ELBOW), m(WRIST), pre + "Arm", (0, -1, 0)),
            (pre + "Hand", m(WRIST), m(HAND_TIP), pre + "ForeArm", (0, -1, 0)),
            (pre + "UpLeg", m(HIP), m(KNEE), "Hips", (0, -1, 0)),
            (pre + "Leg", m(KNEE), m(ANKLE), pre + "UpLeg", (0, -1, 0)),
            (pre + "Foot", m(ANKLE), m(BALL), pre + "Leg", (0, 0, 1)),
            (pre + "ToeBase", m(BALL), m(TOE), pre + "Foot", (0, 0, 1)),
        ]
    return bones


# ---------------------------------------------------------------------------------------------
# Blender object creation
# ---------------------------------------------------------------------------------------------
def make_material(name):
    color, rough = PALETTE[name]
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    try:
        mat.use_nodes = True
    except (AttributeError, TypeError):
        pass
    lin = srgb_to_linear(color)
    mat.diffuse_color = lin
    if mat.node_tree is not None:
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = lin
            bsdf.inputs["Roughness"].default_value = rough
            bsdf.inputs["Metallic"].default_value = 0.0
    return mat


def to_object(mb, arm_obj):
    me = bpy.data.meshes.new(mb.name)
    me.from_pydata([tuple(v) for v in mb.verts], [], mb.faces)
    me.update()
    for poly, mi in zip(me.polygons, mb.face_mats):
        poly.material_index = mi
    try:
        me.shade_smooth()
    except AttributeError:
        for poly in me.polygons:
            poly.use_smooth = True
    for name in mb.materials:
        me.materials.append(make_material(name))
    obj = bpy.data.objects.new(mb.name, me)
    bpy.context.scene.collection.objects.link(obj)

    groups = {}
    for i, w in enumerate(mb.weights):
        for bone, value in w.items():
            if value <= 0.0:
                continue
            groups.setdefault(bone, {}).setdefault(round(value, 5), []).append(i)
    for bone in sorted(groups):
        vg = obj.vertex_groups.new(name=bone)
        for value, idx in groups[bone].items():
            vg.add(idx, value, "REPLACE")

    obj.parent = arm_obj
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm_obj
    return obj


def build_armature():
    arm = bpy.data.armatures.new("Armature")
    obj = bpy.data.objects.new("Armature", arm)
    bpy.context.scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    created = {}
    for name, head, tail, parent, roll_axis in bone_list():
        eb = arm.edit_bones.new(name)
        eb.head = Vector(head)
        eb.tail = Vector(tail)
        eb.align_roll(Vector(roll_axis))
        eb.use_connect = False
        if parent:
            eb.parent = created[parent]
        created[name] = eb
    bpy.ops.object.mode_set(mode="OBJECT")
    return obj


def add_face_shape_keys(face_obj, mb):
    face_obj.shape_key_add(name="Basis", from_mix=False)
    mstart, mend = mb.ranges["mouth"]
    eyes = {"eyeBlinkLeft": (1, mb.ranges["eye_L"]), "eyeBlinkRight": (-1, mb.ranges["eye_R"])}

    def mouth_points(params):
        layers = mouth_layers(params)
        pts = []
        for name, _, _ in MOUTH_LAYER_MATERIALS:
            pts.extend(layers[name])
        return pts

    for key in SHAPE_ORDER:
        kb = face_obj.shape_key_add(name=key, from_mix=False)
        # Rest weight 0: the exporter writes these values as the glTF mesh's default morph
        # weights, and new keys can start at 1.0, which would stack every viseme on the idle face.
        kb.value = 0.0
        if key in eyes:
            side, (start, end) = eyes[key]
            layers = eye_layers(side, 1.0)
            pts = [p for name, _ in EYE_LAYER_MATERIALS for p in layers[name]]
            assert len(pts) == end - start, (key, len(pts), end - start)
            for i, p in enumerate(pts):
                kb.data[start + i].co = p
        else:
            pts = mouth_points(MOUTH_SHAPES[key])
            assert len(pts) == mend - mstart, (key, len(pts), mend - mstart)
            for i, p in enumerate(pts):
                kb.data[mstart + i].co = p


def export_glb(path, objects):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    kwargs = dict(
        filepath=path,
        export_format="GLB",
        use_selection=True,
        export_copyright=COPYRIGHT,
        export_yup=True,
        export_apply=False,
        export_texcoords=False,
        export_normals=True,
        export_tangents=False,
        export_materials="EXPORT",
        export_cameras=False,
        export_lights=False,
        export_extras=True,
        export_skins=True,
        export_morph=True,
        export_morph_normal=False,
        export_morph_tangent=False,
        export_animations=False,
        # One vertex accessor set per mesh shared by all its material primitives. Required:
        # glTFast (6.14) fails to import a skinned mesh whose primitives each bring their own
        # JOINTS/WEIGHTS accessors (job-safety error in SortAndNormalizeBoneWeightsJob).
        export_shared_accessors=True,
    )
    optional = ["export_animations", "export_morph_tangent", "export_tangents", "export_texcoords",
                "export_extras", "export_cameras", "export_lights"]
    while True:
        try:
            bpy.ops.export_scene.gltf(**kwargs)
            return
        except TypeError as exc:  # parameter renamed/removed in this Blender version
            drop = next((k for k in optional if k in kwargs and k in str(exc)), None)
            if drop is None:
                raise
            kwargs.pop(drop)


# ---------------------------------------------------------------------------------------------
# Preview renders (documentation only; never exported)
# ---------------------------------------------------------------------------------------------
def set_engine(scene):
    for engine in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT", "CYCLES"):
        try:
            scene.render.engine = engine
            break
        except TypeError:
            continue
    if scene.render.engine == "CYCLES":
        scene.cycles.samples = 48
        scene.cycles.device = "CPU"
    try:
        scene.view_settings.view_transform = "AgX"
    except TypeError:
        pass


def render_still(scene, path):
    scene.render.filepath = path
    try:
        bpy.ops.render.render(write_still=True)
    except RuntimeError as exc:   # e.g. no GPU context for Eevee in this session
        if scene.render.engine == "CYCLES":
            raise
        print("[ci_companion] %s failed (%s); retrying with Cycles" % (scene.render.engine, exc))
        scene.render.engine = "CYCLES"
        scene.cycles.samples = 48
        scene.cycles.device = "CPU"
        bpy.ops.render.render(write_still=True)


def look_at(obj, target):
    direction = Vector(target) - obj.location
    obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()


def setup_stage(scene, extra_hidden=()):
    world = bpy.data.worlds.new("Preview")
    scene.world = world
    try:
        world.use_nodes = True
    except (AttributeError, TypeError):
        pass
    bg = next((n for n in world.node_tree.nodes if n.type == "BACKGROUND"), None)
    if bg is not None:
        bg.inputs["Color"].default_value = (0.62, 0.68, 0.74, 1.0)
        bg.inputs["Strength"].default_value = 0.55

    def light(name, kind, loc, energy, size, color):
        data = bpy.data.lights.new(name, kind)
        data.energy = energy
        data.color = color
        if kind == "AREA":
            data.size = size
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = loc
        look_at(obj, (0, 0, 1.0))
        return obj

    light("Key", "AREA", (-1.6, -2.2, 2.6), 520.0, 1.6, (1.0, 0.95, 0.9))
    light("Fill", "AREA", (2.0, -1.8, 1.6), 220.0, 2.0, (0.85, 0.92, 1.0))
    light("Rim", "AREA", (0.6, 2.2, 2.4), 300.0, 1.2, (0.9, 1.0, 1.0))

    floor_me = bpy.data.meshes.new("Floor")
    r, n = 4.0, 48
    floor_me.from_pydata([(0, 0, 0)] + [(math.cos(2 * math.pi * i / n) * r, math.sin(2 * math.pi * i / n) * r, 0) for i in range(n)],
                         [], [(0, i + 1, (i + 1) % n + 1) for i in range(n)])
    floor_mat = bpy.data.materials.new("FloorPreview")
    floor_mat.diffuse_color = (0.30, 0.32, 0.34, 1.0)
    try:
        floor_mat.use_nodes = True
    except (AttributeError, TypeError):
        pass
    if floor_mat.node_tree is not None:
        bsdf = next((nd for nd in floor_mat.node_tree.nodes if nd.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            bsdf.inputs["Base Color"].default_value = (0.30, 0.32, 0.34, 1.0)
            bsdf.inputs["Roughness"].default_value = 0.9
    floor_me.materials.append(floor_mat)
    floor = bpy.data.objects.new("Floor", floor_me)
    scene.collection.objects.link(floor)
    return floor


def render_full_body(path):
    scene = bpy.context.scene
    set_engine(scene)
    scene.render.resolution_x, scene.render.resolution_y = 960, 540
    scene.render.film_transparent = False
    setup_stage(scene)
    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_data.lens = 35
    cam = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (1.10, -3.70, 1.10)
    look_at(cam, (0.0, 0.0, 0.84))
    scene.camera = cam
    render_still(scene, path)


def render_viseme_sheet(path, face_obj):
    """Renders the face with a row of expressions side by side (documentation)."""
    scene = bpy.context.scene
    keys = ["Basis", "viseme_aa", "viseme_E", "viseme_I", "viseme_O", "viseme_U", "viseme_SS", "viseme_FF", "eyeBlinkLeft"]
    copies = []
    spacing = 0.26
    for i, key in enumerate(keys):
        dup = face_obj.copy()
        dup.data = face_obj.data.copy()
        dup.modifiers.clear()
        dup.parent = None
        dup.location = ((i - (len(keys) - 1) / 2) * spacing, -3.0, 0.0)
        scene.collection.objects.link(dup)
        if dup.data.shape_keys:
            for kb in dup.data.shape_keys.key_blocks:
                kb.value = 1.0 if kb.name == key else 0.0
        copies.append(dup)
    cam_data = bpy.data.cameras.new("SheetCam")
    cam_data.type = "ORTHO"
    cam_data.ortho_scale = spacing * len(keys)
    cam = bpy.data.objects.new("SheetCam", cam_data)
    scene.collection.objects.link(cam)
    cam.location = (0.0, -4.2, HEAD_C.z - 0.01)
    look_at(cam, (0.0, -3.0, HEAD_C.z - 0.01))
    scene.camera = cam
    hidden = []
    for o in scene.objects:
        if o.type == "MESH" and o not in copies:
            o.hide_render = True
            hidden.append(o)
    scene.render.resolution_x, scene.render.resolution_y = 1800, 280
    render_still(scene, path)
    for o in hidden:
        o.hide_render = False
    for o in copies:
        bpy.data.objects.remove(o, do_unlink=True)


# ---------------------------------------------------------------------------------------------
def main(argv):
    ap = argparse.ArgumentParser(prog="build_ci_companion.py")
    ap.add_argument("--out", required=True, help="GLB output path")
    ap.add_argument("--render", help="optional full-body preview PNG")
    ap.add_argument("--visemes", help="optional expression sheet PNG")
    ap.add_argument("--blend", help="optional .blend to save for inspection")
    args = ap.parse_args(argv)
    # Resolve against the shell's working directory: Blender resolves relative render paths
    # against the (unsaved) .blend's location, which is the drive root in a factory session.
    for name in ("out", "render", "visemes", "blend"):
        if getattr(args, name):
            setattr(args, name, os.path.abspath(getattr(args, name)))

    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene["generator"] = "tools/avatar/build_ci_companion.py"
    scene["license"] = "Proprietary (LicenseRef-CI-Commercial-1.0), see LICENSE.md"
    scene["origin"] = "Procedurally generated by Companion Intelligence; no third-party assets"

    arm_obj = build_armature()

    body = MeshBuilder("Body")
    build_body(body)
    face = MeshBuilder("Face")
    build_face(face)

    body_obj = to_object(body, arm_obj)
    face_obj = to_object(face, arm_obj)
    add_face_shape_keys(face_obj, face)

    print("[ci_companion] Body: %d verts / %d faces / %d materials" % (len(body.verts), len(body.faces), len(body.materials)))
    print("[ci_companion] Face: %d verts / %d faces / %d materials / %d shape keys" %
          (len(face.verts), len(face.faces), len(face.materials), len(face_obj.data.shape_keys.key_blocks) - 1))

    export_glb(args.out, [arm_obj, body_obj, face_obj])
    print("[ci_companion] exported", args.out)

    if args.blend:
        bpy.ops.wm.save_as_mainfile(filepath=args.blend)
    if args.render:
        render_full_body(args.render)
        print("[ci_companion] rendered", args.render)
    if args.visemes:
        render_viseme_sheet(args.visemes, face_obj)
        print("[ci_companion] rendered", args.visemes)


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    main(argv)
