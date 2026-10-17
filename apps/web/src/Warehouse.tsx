import { useEffect, useRef, useState } from "react";
import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { GLTFLoader } from "three/addons/loaders/GLTFLoader.js";
type Props = {
  orders: any[];
  selected?: string;
  onSelect: (id: string) => void;
  replay: boolean;
};
export default function Warehouse({
  orders,
  selected,
  onSelect,
  replay,
}: Props) {
  const host = useRef<HTMLDivElement>(null);
  const [unavailable, setUnavailable] = useState(false);
  const state = useRef({ orders, selected, onSelect, replay });
  state.current = { orders, selected, onSelect, replay };
  useEffect(() => {
    const el = host.current;
    if (!el) return;
    let renderer: THREE.WebGLRenderer;
    try {
      renderer = new THREE.WebGLRenderer({
        antialias: true,
        alpha: true,
        powerPreference: "low-power",
      });
    } catch {
      setUnavailable(true);
      return;
    }
    const scene = new THREE.Scene();
    scene.background = new THREE.Color("#eaf1ed");
    scene.fog = new THREE.Fog("#eaf1ed", 42, 95);
    const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 200);
    camera.position.set(27, 24, 31);
    renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    renderer.outputColorSpace = THREE.SRGBColorSpace;
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.35;
    el.appendChild(renderer.domElement);
    const controls = new OrbitControls(camera, renderer.domElement);
    controls.target.set(0, 1, 0);
    controls.enableDamping = true;
    controls.maxPolarAngle = Math.PI / 2.3;
    controls.minDistance = 18;
    controls.maxDistance = 65;
    controls.enablePan = true;
    scene.add(new THREE.HemisphereLight(0xffffff, 0x809b8e, 3));
    const sun = new THREE.DirectionalLight(0xffffff, 4);
    sun.position.set(14, 25, 10);
    sun.castShadow = true;
    sun.shadow.mapSize.set(2048, 2048);
    sun.shadow.camera.left = -25;
    sun.shadow.camera.right = 25;
    sun.shadow.camera.top = 25;
    sun.shadow.camera.bottom = -25;
    sun.shadow.normalBias = 0.04;
    scene.add(sun);
    const materials: THREE.Material[] = [];
    const geometries: THREE.BufferGeometry[] = [];
    const objects: THREE.Object3D[] = [];
    function box(
      w: number,
      h: number,
      d: number,
      x: number,
      y: number,
      z: number,
      color: string,
      parent: THREE.Object3D = scene,
    ) {
      const g = new THREE.BoxGeometry(w, h, d);
      const m = new THREE.MeshStandardMaterial({ color, roughness: 0.88 });
      geometries.push(g);
      materials.push(m);
      const mesh = new THREE.Mesh(g, m);
      mesh.position.set(x, y, z);
      mesh.castShadow = true;
      mesh.receiveShadow = true;
      parent.add(mesh);
      return mesh;
    }
    function text(
      label: string,
      x: number,
      y: number,
      z: number,
      size = 0.8,
      color = "#0f2c2c",
    ) {
      const canvas = document.createElement("canvas");
      canvas.width = 512;
      canvas.height = 96;
      const c = canvas.getContext("2d")!;
      c.fillStyle = color;
      c.font = "bold 44px sans-serif";
      c.textAlign = "center";
      c.fillText(label, 256, 64);
      const texture = new THREE.CanvasTexture(canvas);
      const m = new THREE.SpriteMaterial({ map: texture });
      materials.push(m);
      const sprite = new THREE.Sprite(m);
      sprite.position.set(x, y, z);
      sprite.scale.set(size * 4, size * 0.75, 1);
      scene.add(sprite);
      return sprite;
    }
    box(36, 0.22, 29, 0, -0.1, 0, "#dce6df");
    box(20, 0.15, 11, -2, 0.1, 2, "#d2ded6");
    box(16, 5.2, 8, -2, 2.7, -5, "#c7d8cd");
    box(16.5, 0.25, 8.5, -2, 5.45, -5, "#fbfdfb");
    box(16.6, 0.16, 0.18, -2, 4.6, -0.89, "#078656");
    for (let i = 0; i < 5; i++) {
      const x = -8.1 + i * 3.1;
      box(2.2, 3.5, 0.18, x, 1.82, -0.93, "#24483e");
      for (let n = 0; n < 9; n++)
        box(2.1, 0.035, 0.06, x, 0.4 + n * 0.35, -0.82, "#809c8d");
      box(2.6, 0.15, 1.9, x, 0.12, 0.1, "#f9fcfa");
      text("0" + (i + 1), x, 4.05, -0.65, 0.35, "#ffffff");
      box(0.08, 0.12, 6.5, x - 1.45, 0.03, 4.1, "#ffffff");
    }
    for (let i = 0; i < 6; i++) {
      box(1.7, 2.9, 2.9, -8.8 + i * 2.7, 6.95, -5, "#b8cec0");
      box(1.75, 0.14, 2.95, -8.8 + i * 2.7, 8.47, -5, "#eef4ef");
    }
    for (let row = 0; row < 3; row++)
      for (let col = 0; col < 4; col++) {
        const x = 9 + col * 1.15,
          z = -4 + row * 1.65;
        box(1, 0.14, 1.2, x, 0.15, z, "#98ad9d");
        box(0.9, 0.85, 1.05, x, 0.64, z, "#c3d7c8");
        box(0.045, 0.86, 1.06, x, 0.64, z, "#e1ebe3");
      }
    box(0.12, 0.12, 20, 14.4, 0.05, 2, "#ffffff");
    for (let i = 0; i < 10; i++)
      box(0.15, 0.03, 0.8, 14, 0.1, -5 + i * 2, "#a7b7ac");
    box(27, 0.03, 0.16, -0.5, 0.05, 10, "#0a8656");
    box(0.16, 0.03, 6, -14, 0.05, 7, "#0a8656");
    for (const [x, z] of [
      [-15, -9],
      [13, -10],
      [15, 10],
      [-16, 9],
    ]) {
      box(0.18, 2.2, 0.18, x, 1.1, z, "#7c9a87");
      const geo = new THREE.SphereGeometry(1.15, 8, 6);
      geometries.push(geo);
      const mat = new THREE.MeshStandardMaterial({
        color: "#b8d4bf",
        roughness: 1,
      });
      materials.push(mat);
      const crown = new THREE.Mesh(geo, mat);
      crown.position.set(x, 3, z);
      crown.castShadow = true;
      scene.add(crown);
    }
    const fleet = new THREE.Group();
    scene.add(fleet);
    let ids = "";
    let disposed = false;
    let vehicleTemplate: THREE.Object3D | null = null;
    let vehicleAnimations: THREE.AnimationClip[] = [];
    let mixers: THREE.AnimationMixer[] = [];
    const modelTextures = new Set<THREE.Texture>();
    new GLTFLoader().load("/models/delivery-truck.glb", (gltf) => {
      const model = gltf.scene;
      model.traverse((object) => {
        if (!(object instanceof THREE.Mesh)) return;
        object.castShadow = true;
        object.receiveShadow = true;
        geometries.push(object.geometry);
        const source = Array.isArray(object.material) ? object.material : [object.material];
        for (const material of source) {
          materials.push(material);
          if (material instanceof THREE.MeshStandardMaterial && material.map) modelTextures.add(material.map);
        }
        // Recolor the licensed demonstration body and remove its original logo texture.
        if (source.some(material => material.name === "truck")) {
          const body = new THREE.MeshStandardMaterial({ name: "Toushir demonstration body", color: "#f3fff8", roughness: .62, metalness: .12 });
          materials.push(body); object.material = body;
        }
      });
      if (disposed) {
        modelTextures.forEach(texture => texture.dispose());
        model.traverse(object => { if (object instanceof THREE.Mesh) object.geometry.dispose(); });
        materials.forEach(material => material.dispose());
        return;
      }
      const initial = new THREE.Box3().setFromObject(model).getSize(new THREE.Vector3());
      if (initial.x > initial.z) model.rotation.y = Math.PI / 2;
      const bounds = new THREE.Box3().setFromObject(model), size = bounds.getSize(new THREE.Vector3()), center = bounds.getCenter(new THREE.Vector3());
      const scale = 5.5 / Math.max(size.x, size.z);
      model.scale.setScalar(scale);
      model.position.set(-center.x * scale, -bounds.min.y * scale, -center.z * scale);
      const normalized = new THREE.Group(); normalized.add(model);
      vehicleTemplate = normalized;
      vehicleAnimations = gltf.animations;
      el.dataset.vehicleModel = "loaded";
      ids = "";
    }, undefined, () => { el.dataset.vehicleModel = "fallback"; });
    let simulationStart = performance.now();
    function rebuild() {
      mixers.forEach(mixer => mixer.stopAllAction()); mixers = [];
      for (const c of [...fleet.children]) fleet.remove(c);
      objects.length = 0;
      const active = state.current.orders
        .filter((o) =>
          ["CONFIRMED", "PREPARING", "READY", "OUT_FOR_DELIVERY"].includes(
            o.status,
          ),
        )
        .slice(0, 6);
      for (let i = 0; i < active.length; i++) {
        const order = active[i];
        const truck = new THREE.Group();
        truck.userData = { orderId: order.id, index: i, baseX: -8 + i * 3.6 };
        truck.position.set(-8 + i * 3.6, 0.05, 3.7);
        box(2, 1.7, 4.4, 0, 1.22, 0, "#f9fbf8", truck);
        box(
          2.05,
          1.4,
          1.45,
          0,
          1.1,
          2.65,
          i % 2 === 0 ? "#06dd81" : "#0f6047",
          truck,
        );
        box(1.78, 0.52, 0.05, 0, 1.6, 3.4, "#234840", truck);
        box(2.1, 0.25, 0.25, 0, 0.45, 3.45, "#d6e5dc", truck);
        for (const x of [-1.07, 1.07])
          for (const z of [-1.5, 2.45]) {
            const g = new THREE.CylinderGeometry(0.36, 0.36, 0.22, 16);
            geometries.push(g);
            const m = new THREE.MeshStandardMaterial({ color: "#263e33" });
            materials.push(m);
            const wheel = new THREE.Mesh(g, m);
            wheel.rotation.z = Math.PI / 2;
            wheel.position.set(x, 0.38, z);
            truck.add(wheel);
          }
        const markerGeo = new THREE.SphereGeometry(0.19, 16, 12);
        geometries.push(markerGeo);
        const markerMat = new THREE.MeshStandardMaterial({
          color: order.id === state.current.selected ? "#06dd81" : "#ffffff",
          emissive: "#087f4f",
          emissiveIntensity: 0.1,
        });
        materials.push(markerMat);
        const marker = new THREE.Mesh(markerGeo, markerMat);
        marker.position.set(0, 3.6, 0);
        if (vehicleTemplate) {
          for (const child of [...truck.children]) truck.remove(child);
          const vehicle = vehicleTemplate.clone(true);
          truck.add(vehicle);
          const mixer = new THREE.AnimationMixer(vehicle);
          vehicleAnimations.forEach(clip => mixer.clipAction(clip).play());
          mixers.push(mixer);
        }
        truck.add(marker);
        fleet.add(truck);
        objects.push(truck);
      }
    }
    function resize() {
      const r = el!.getBoundingClientRect();
      camera.aspect = r.width / Math.max(r.height, 1);
      camera.updateProjectionMatrix();
      renderer.setSize(r.width, r.height, false);
    }
    const observer = new ResizeObserver(resize);
    observer.observe(el);
    resize();
    const raycaster = new THREE.Raycaster();
    const mouse = new THREE.Vector2();
    let downX = 0,
      downY = 0;
    const down = (e: PointerEvent) => {
      downX = e.clientX;
      downY = e.clientY;
    };
    const pick = (e: PointerEvent) => {
      if (Math.hypot(e.clientX - downX, e.clientY - downY) > 6) return;
      const r = el!.getBoundingClientRect();
      mouse.set(
        ((e.clientX - r.left) / r.width) * 2 - 1,
        (-(e.clientY - r.top) / r.height) * 2 + 1,
      );
      raycaster.setFromCamera(mouse, camera);
      const hit = raycaster.intersectObjects(objects, true)[0];
      if (hit) {
        let o: THREE.Object3D | null = hit.object;
        while (o && !o.userData.orderId) o = o.parent;
        if (o) state.current.onSelect(o.userData.orderId);
      }
    };
    renderer.domElement.addEventListener("pointerdown", down);
    renderer.domElement.addEventListener("pointerup", pick);
    const reduced = matchMedia("(prefers-reduced-motion: reduce)").matches;
    let frame = 0;
    let lastFrame = performance.now();
    function animate() {
      frame = requestAnimationFrame(animate);
      const currentFrame = performance.now(), delta = Math.min(.1, (currentFrame - lastFrame) / 1000); lastFrame = currentFrame;
      const next =
        state.current.orders.map((o) => o.id + o.status).join() +
        "|" +
        state.current.selected;
      if (next !== ids) {
        ids = next;
        rebuild();
      }
      if (state.current.replay && !reduced) {
        mixers.forEach(mixer => mixer.update(delta));
        const t = (performance.now() - simulationStart) / 7000;
        for (const truck of fleet.children) {
          const i = truck.userData.index;
          const phase = (t + i * 0.17) % 1;
          truck.position.z =
            phase < 0.55 ? 3.7 + phase * 15 : 12 - (phase - 0.55) * 18;
          truck.position.x =
            truck.userData.baseX + Math.max(0, phase - 0.55) * 14;
          truck.rotation.y = phase > 0.55 ? Math.PI / 2 : 0;
        }
      } else {
        for (const truck of fleet.children) {
          truck.position.set(truck.userData.baseX, 0.05, 3.7);
          truck.rotation.y = 0;
        }
      }
      controls.update();
      renderer.render(scene, camera);
    }
    animate();
    return () => {
      disposed = true;
      cancelAnimationFrame(frame);
      mixers.forEach(mixer => mixer.stopAllAction());
      observer.disconnect();
      controls.dispose();
      renderer.dispose();
      geometries.forEach((g) => g.dispose());
      materials.forEach((m) => {
        if (m instanceof THREE.SpriteMaterial) m.map?.dispose();
        m.dispose();
      });
      modelTextures.forEach(texture => texture.dispose());
      renderer.domElement.remove();
    };
  }, []);
  return (
    <div
      className="warehouse"
      ref={host}
      aria-label="Entrepôt 3D interactif. Glissez pour pivoter, pincez ou utilisez la molette pour zoomer."
    >
      {unavailable && (
        <div className="empty">
          <strong>La vue 3D n’est pas disponible sur cet appareil.</strong>
          <p>
            Utilisez la liste des livraisons pour suivre les mêmes opérations.
          </p>
        </div>
      )}
      <div className="map-label depot">
        <span className="live-dot" />
        D01 · Dépôt TOUSHIR<small>Préparation et expédition</small>
      </div>
      <div className="map-hint">
        Glisser pour pivoter · Molette pour zoomer · Cliquer sur un véhicule
        <small>Modèle d’illustration adapté de Cesium · CC BY 4.0 · positions illustratives</small>
      </div>
    </div>
  );
}
