# Implementación por fases — estado y cómo probar

Cada fase está en **su propia rama**, encadenada a la anterior. Si una falla, las anteriores siguen sirviendo.

```
maui ── diseno-entrenador ── fase1-base-datos ── fase2-ajustes ── fase3-multimedia
        (diseño y maquetas)   (BD + tokens)       (Ajustes,        (vídeos e imágenes)
                                                   vistas,
                                                   prescripción)
```

Para probar una fase: `git checkout <rama>`, compilar y ejecutar en el móvil. La última rama incluye todo lo anterior.

## Cómo se ha verificado (sin Android)

En la nube no hay SDK de Android, así que:

* **Pruebas automáticas** (`SteelProgress.Tests`, 58): migraciones sobre una base de datos con datos antiguos, lógica de vistas, ajustes, prescripción y multimedia. Se ejecutan con `dotnet test SteelProgress.Tests`.
* **Compilación de la app móvil** como `net10.0` (sin Android) para validar todo el C# y el XAML de `SteelProgress.Maui`. Detectó y corrigió claves de recursos duplicadas que habrían roto la compilación.
* **Enlaces de datos** de las pantallas nuevas comprobados con bindings tipados.

Lo que **no** se ha podido comprobar: cómo se ve, el comportamiento táctil, la galería y la cámara del móvil, y la compilación real para Android/iOS.

---

## Fase 1 — `fase1-base-datos`

**Qué hace**
* Migración `AddSharingAndPrescription`: identificador único (`Uid`) en ejercicios, rutinas, días, ejercicios de día y sesiones (se rellena solo para los datos existentes); prescripción ampliada (rango de reps, peso/%1RM, RPE/RIR, tempo, descanso, calentamiento, superserie, notas); sesión con duración, sensación, notas y si quedó completa; serie con RPE, calentamiento y nota; perfil con vista simple/entrenador, bloques y «Yo también entreno».
* Sistema de diseño: `Resources/Styles/Tokens.xaml` (colores, degradado acero, radios, espaciado, estilos de texto, tarjetas, chips y botones) y fuente Space Grotesk. No cambia ninguna pantalla por sí solo.

**Probar**
1. Abrir la app con tus datos actuales: debe actualizar la base de datos al arrancar y mostrar todo igual que antes.

## Fase 2 — `fase2-ajustes`

**Qué hace**
* **Ajustes** (engranaje en Inicio): vista simple o entrenador, bloques de la vista simple y «Yo también entreno». Se guarda al momento. Primera pantalla con el sistema de diseño.
* **Rutinas → Ejercicios**: cada ejercicio muestra lo prescrito («4 × 6–8 · RPE 8 · 1:30») y el botón «Editar series, reps e intensidad» abre un panel inferior. Los campos avanzados aparecen según la vista y los bloques, o si ya tienen valor (la vista simple nunca esconde datos).
* **Entreno**: debajo del ejercicio seleccionado, el objetivo y la nota del entrenador.

**Probar**
1. Inicio → engranaje → cambiar entre Simple y Entrenador; activar y desactivar bloques; salir y volver: debe recordarse.
2. Rutinas → elegir rutina, día y ejercicio → «Editar series…». En vista simple solo series, reps y notas; activar «Intensidad» en Ajustes y volver: aparece la sección. En vista entrenador, todo.
3. Escribir valores (probar «72,5» con coma) y guardar; ver el texto bajo el ejercicio. Probar un valor incorrecto (RPE 11): debe avisar sin guardar.
4. Iniciar un entreno de ese día: debe verse «Objetivo: …» y la nota.

## Fase 3 — `fase3-multimedia`

**Qué hace**
* Migración `AddExerciseMedia` y almacenamiento en la carpeta privada de la app (`AppDataDirectory/media`), con nombres generados, solo formatos admitidos y máximo de 50 MB.
* **Ejercicios**: en el formulario, sección «Vídeos e imágenes» para elegir de la galería (varios a la vez), grabar un vídeo si el móvil lo permite y quitar. Miniatura en la lista. Al cancelar un ejercicio nuevo se borran los archivos elegidos; al borrar un ejercicio, también.
* **Entreno**: botón «▶ Ver cómo se hace» si el ejercicio tiene vídeo o imagen.
* El vídeo se abre con el reproductor del móvil. El reproductor dentro de la app depende de la decisión **D1** (añadir `CommunityToolkit.Maui.MediaElement`), pendiente de tu aprobación.
* Permisos de Android (cámara, micrófono, almacenamiento en Android ≤ 12) y textos de privacidad de iOS.

**Probar**
1. Ejercicios → «+» → «Añadir vídeo o imagen» → elegir un vídeo, un GIF y una foto → guardar. Ver la miniatura en la lista.
2. Tocar la miniatura del vídeo en el formulario: debe abrirse el reproductor.
3. Quitar uno con la ✕ y volver a entrar: no debe estar.
4. Crear un ejercicio, añadir un vídeo y cerrar sin guardar: no debe quedar nada.
5. Un vídeo de más de 50 MB: debe avisar.
6. En un entreno con ese ejercicio: «▶ Ver cómo se hace».

---

## Pendiente de decidir

* **D1** — reproductor de vídeo dentro de la app (`CommunityToolkit.Maui.MediaElement`, dependencia nueva) o seguir con el del sistema.
* **D6** — biblioteca de ejercicios precargada.
* Límite de vídeo configurable en Ajustes (hoy fijo en 50 MB).

## Notas técnicas

* `Directory.Build.props` usa `C:\SPBuild\` (ruta de Windows). Para compilar en Linux se usa una copia temporal con otra ruta; no hace falta cambiar nada en tu PC.
* `RoutineViewModel` y `ExerciseViewModel` aceptan sus servicios nuevos como opcionales para que la app de escritorio (WPF), que los crea a mano, siga compilando.
* Corregido durante las pruebas: al quitar un vídeo de un ejercicio cargado, EF Core lo «resucitaba» en vez de borrarlo (`ExerciseRepository.RemoveMediaAsync`).
