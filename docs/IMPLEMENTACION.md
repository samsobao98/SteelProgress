# Implementación por fases — estado y cómo probar

Cada fase está en **su propia rama**, encadenada a la anterior. Si una falla, las anteriores siguen sirviendo.

```
maui ── diseno-entrenador ── fase1-base-datos ── fase2-ajustes ── fase3-multimedia ── fase4-compartir ── fase5-clientes
        (diseño y maquetas)   (BD + tokens)       (Ajustes,        (vídeos e           (exportar e         (clientes del
                                                   vistas,          imágenes)           importar .steel)    entrenador)
                                                   prescripción)
```

Para probar una fase: `git checkout <rama>`, compilar y ejecutar en el móvil. La última rama incluye todo lo anterior.

## Cómo se ha verificado (sin Android)

En la nube no hay SDK de Android, así que:

* **Pruebas automáticas** (`SteelProgress.Tests`, 85): migraciones sobre una base de datos con datos antiguos, lógica de vistas, ajustes, prescripción y multimedia. Se ejecutan con `dotnet test SteelProgress.Tests`.
* **Compilación de la app móvil** como `net10.0` (sin Android) para validar todo el C# y el XAML de `SteelProgress.Maui`. Detectó y corrigió claves de recursos duplicadas que habrían roto la compilación.
* **Enlaces de datos** de las pantallas nuevas comprobados con bindings tipados.
* **Recursos** (`StaticResource`) de todas las pantallas comprobados con un script: un recurso que falta no lo detecta el compilador y haría fallar la pantalla al abrirse.
* **Código de Android** (`MainActivity`) compilado contra las librerías de referencia de Android, con MAUI simulado.

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

## Fase 4 — `fase4-compartir`

**Qué hace**
* Archivo **`.steel`**: un ZIP con `manifest.json` (autor, mensaje, fecha, si lleva vídeos), `data.json` (ejercicios y rutinas con su prescripción) y `media/` si se exporta con vídeos.
* **Rutinas → Compartir**: con la rutina seleccionada, elegir con o sin vídeos (se ve el tamaño), escribir un mensaje opcional y compartir con el menú del móvil (WhatsApp, correo, Drive…).
* **Rutinas → Importar**: elegir un `.steel`; se abre la **vista previa** con autor, mensaje, días, ejercicios nuevos, vídeos y, si ya la tienes, «Actualización vX → vY».
* **Importar** reutiliza los ejercicios que ya tienes (por su `Uid`), solo trae los vídeos que faltan y:
  * **Actualizar rutina**: cambia la rutina que ya tienes sin perder historial (los días se reutilizan por orden y un día con entrenos nunca se borra);
  * **Guardar como copia**: crea otra rutina («Nombre (v4)»).
* Si un ejercicio importado se llama igual que uno tuyo distinto, entra como «Nombre (2)».
* Si algo falla a mitad, no queda nada a medias (ni datos ni archivos).
* Migración `MakeUidsUniquePerOwner`: el `Uid` de ejercicios y rutinas pasa a ser único por perfil, para que dos perfiles del mismo móvil puedan importar el mismo archivo.
* **Abrir desde otras apps (Android)**: tocar un `.steel` en WhatsApp o en el correo y elegir SteelProgress. Si aún no has elegido perfil, se abre al elegirlo. Va en su propio commit (`ce629f9`): si diera problemas al compilar o abrir, se puede revertir solo ese.

**Probar**
1. Rutinas → seleccionar una rutina → «↗ Compartir» → con vídeos → escribir un mensaje → enviártelo (por ejemplo, a ti mismo por WhatsApp o a Drive).
2. En el mismo móvil, «↓ Importar» → elegir el archivo: debe decir que ya la tienes («Actualización v1 → v1») y ofrecer guardarla como copia.
3. Con otro perfil (o en otro móvil): importar → debe crear la rutina con sus ejercicios, prescripción y vídeos.
4. Cambiar algo de la rutina original, subir su versión (de momento no hay botón: se hará en la fase 7) y volver a compartir para probar «Actualizar».
5. Tocar el `.steel` desde WhatsApp o un gestor de archivos → «Abrir con SteelProgress».
6. Un archivo que no sea `.steel`: debe avisar.

## Fase 5 — `fase5-clientes`

**Qué hace**
* Migración `AddClients`: tabla de clientes (cuelgan del perfil del entrenador), y rutinas y sesiones con `ClientId`; las sesiones registradas por el entrenador con el cliente delante quedan marcadas como **presenciales**.
* **Cliente activo**: en lugar de duplicar pantallas, el entrenador «entra» en un cliente y **Rutinas, Entreno e Historial** pasan a mostrar y guardar los datos de ese cliente. Un aviso «Trabajando con Marta · Salir» lo indica y, al salir, todo vuelve a tus datos. Durante un entreno no se puede salir.
* Tus estadísticas de Inicio no cuentan las sesiones de clientes.
* **Pestaña Clientes** (solo en vista entrenador): lista con objetivo, rutina, sesiones de la semana y aviso en rojo si lleva 7 días o más sin entrenar. Al tocar uno: entrenar con él, ver sus rutinas, ver su historial, editar o eliminar (borra sus rutinas y sesiones, no las tuyas).
* En vista entrenador, **Historial** solo aparece si el perfil «también entrena» (Ajustes).
* **Importar** con un cliente activo crea la rutina para ese cliente.

**Probar**
1. Ajustes → Entrenador: aparece la pestaña Clientes (y desaparece Historial si «Yo también entreno» está apagado).
2. Clientes → «+ Cliente» → nombre, objetivo, color → Guardar.
3. Tocar el cliente → «Rutinas de…»: la lista sale vacía y con el aviso arriba. Crear una rutina para él. «Salir»: vuelven tus rutinas.
4. Tocar el cliente → «Entrenar con…» → hacer un entreno. Ver «Historial de…» y comprobar que no aparece en tu historial ni en tus números de Inicio.
5. Volver a Clientes: el cliente muestra «1 esta semana».
6. Cambiar de perfil con un cliente activo: debe salir del cliente.

**Aún no está (para siguientes pasos)**
* «Enviar rutina» directo desde la ficha del cliente (hoy: entrar en el cliente → Rutinas → Compartir).
* Ficha de cliente completa de la maqueta (pestañas Sesiones/Rutinas/Progreso/Notas, adherencia, récords).
* Copiar ejercicios entre perfiles del mismo móvil.
* En vista entrenador sin «Yo también entreno», la pestaña Entreno aún permite entrenos propios si no hay cliente activo.
* Fase 6: devolver sesiones al entrenador (paquete `sessions`) y fase 7: detección automática de cambios y versión al reenviar.

---

## Pendiente de decidir

* **D1** — reproductor de vídeo dentro de la app (`CommunityToolkit.Maui.MediaElement`, dependencia nueva) o seguir con el del sistema.
* **D6** — biblioteca de ejercicios precargada.
* Límite de vídeo configurable en Ajustes (hoy fijo en 50 MB).

## Notas técnicas

* `Directory.Build.props` usa `C:\SPBuild\` (ruta de Windows). Para compilar en Linux se usa una copia temporal con otra ruta; no hace falta cambiar nada en tu PC.
* `RoutineViewModel` y `ExerciseViewModel` aceptan sus servicios nuevos como opcionales para que la app de escritorio (WPF), que los crea a mano, siga compilando.
* Corregido durante las pruebas: al quitar un vídeo de un ejercicio cargado, EF Core lo «resucitaba» en vez de borrarlo (`ExerciseRepository.RemoveMediaAsync`). Lo mismo se evita al quitar días en una actualización de rutina.
* Ojo: la asignación automática del perfil a los datos nuevos (`AppDbContext.SaveChangesAsync`) solo actúa con `SaveChangesAsync`, no con `SaveChanges`. El código nuevo usa siempre la versión asíncrona.
