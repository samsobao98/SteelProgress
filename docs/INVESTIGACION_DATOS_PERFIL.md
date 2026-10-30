# Datos del perfil y de la ficha de cliente: qué aporta algo fiable

Investigación del 2026-10-30, paso 4 del plan de Perfil. Pregunta: ¿qué datos (peso, altura, objetivo, lesiones…) merece la pena guardar, sobre todo para un entrenador, y qué puede hacer la app con ellos sin riesgo? Criterio de Samuel: es la salud de las personas, así que todo lo que suene a consejo o mejora tiene que estar respaldado; y para una app de entreno, fiabilidad y comodidad por encima de todo.

## Regla de tres niveles

| Nivel | Qué es | Ejemplos | Cómo se trata |
|---|---|---|---|
| **1. Dato** | Lo que alguien apuntó | Peso, series, lesión anotada, días disponibles | Sin riesgo: se enseña tal cual |
| **2. Cálculo con fuente** | Una fórmula conocida aplicada a datos | 1RM estimado, media de peso, series por semana | Siempre como «estimado» o «media», con sus límites, y solo donde la fórmula vale |
| **3. Recomendación** | La app dice qué hacer | «Sube peso», «evita este ejercicio», «come menos» | **No la hace la app.** Como mucho enseña una referencia publicada (con su fuente) y decide el entrenador |

Cada dato nuevo tiene que pasar además el filtro de comodidad: si apuntarlo cuesta más de dos toques, se queda vacío, y un dato vacío o viejo engaña más que no tenerlo.

---

## 1. Lo primero: algo que la app ya hace y conviene corregir

**El 1RM estimado se calcula con cualquier número de repeticiones.** `WorkoutRepository.EstimatedOneRepMax` usa Epley (`peso × (1 + reps/30)`) con todas las series que no son de calentamiento, y de ahí salen el «1RM est.» de la ficha del ejercicio y la línea lila de Progreso.

- Los estudios de validación coinciden en que estas fórmulas solo son razonablemente precisas **hasta unas 10 repeticiones**: LeSuer et al. (1997) vio que eran más precisas por debajo de 10, y Reynolds, Gordon y Robergs (2006) recomiendan **no usar más de 10 repeticiones**, con la 5RM como mejor predictor.
- Además asumen que la serie fue **al fallo o casi**; una serie de 12 con 4 en reserva no dice lo mismo.
- Con series de 15–20 (habituales en hipertrofia o en clientes principiantes) el número sale inflado y la gráfica puede subir sin que la fuerza haya subido.

**Propuesta:** calcular el 1RM estimado solo con series de **1 a 10 repeticiones**. Si un ejercicio no tiene ninguna, enseñar el mejor peso × repeticiones y no un 1RM. Mantener la etiqueta «estimado». Es un cambio pequeño y hace la app más honesta sin quitar nada útil.

(Ajustar con el RIR apuntado, contando «reps + RIR» como si fuera al fallo, es práctica común en powerlifting, pero no he encontrado una validación formal. No lo haría por ahora.)

---

## 2. Dato por dato

### Peso corporal (con historial) → **Sí, en perfil personal y en la ficha de cliente**

- **Fiabilidad:** la báscula es precisa, pero el peso de un día no lo es como señal. Varía de un día a otro por agua, glucógeno (cada gramo retiene unos 3–4 g de agua), sal y digestión.
- **Lo seguro:**
  - Apuntarlo (nivel 1).
  - Enseñar la **media de la semana** o una **línea de tendencia** (media móvil exponencial, el método de *The Hacker's Diet*, que suaviza con un factor de alrededor de 0,1 por día) (nivel 2).
  - **Fuerza relativa de la misma persona en el tiempo**: «press de banca = 1,1 × tu peso». Es un cociente simple que se explica solo.
- **Nunca:**
  - Objetivos de calorías.
  - «Deberías pesar X».
  - Juicios sobre el peso.
- **Comodidad:** un campo en la ficha o en Perfil, «Peso de hoy», con el último valor ya puesto.

### Comparar fuerza entre personas (DOTS, Wilks, IPF GL, exponente alométrico 0,67) → **No**

- DOTS, Wilks e IPF GL son fórmulas de **competición de powerlifting**, ajustadas con datos de élite. Ni siquiera las fuentes coinciden en cuál usa hoy la IPF.
- El exponente alométrico 0,67 sale de la teoría. El único estudio práctico que he encontrado (Külkamp et al., 2012, con 22 personas) vio que el exponente real variaba entre 0,35 y 1,22 según el ejercicio y el sexo, y que el ranking cambiaba según el método.
- Un ranking de clientes con estas fórmulas parecería riguroso sin serlo. Tampoco usaría tablas de «nivel intermedio / avanzado» por peso corporal, porque no he encontrado ninguna validada.

### Altura → **No, salvo que se use para algo**

- Su único uso habitual es el IMC, y el IMC **clasifica mal a la gente musculada**. En un estudio con deportistas adolescentes, el 62 % de los que el IMC marcaba como obesos no lo eran según los pliegues cutáneos.
- En una app de fuerza, calcular el IMC daría avisos falsos justo a los usuarios que mejor progresan. Sin IMC, la altura no aporta nada.

### Composición corporal (% de grasa, perímetros) → **Opcional, solo como dato y siempre con el método**

- Los métodos no coinciden entre sí:
  - Los pliegues dan resultados distintos según la ecuación.
  - La bioimpedancia y los pliegues no son intercambiables.
  - Los pliegues siguen mal los cambios de masa magra.
- Lo correcto es comparar **cada persona consigo misma y con el mismo método**.
- Si se añade: un campo «medición» con fecha, valor y método («báscula de bioimpedancia», «pliegues», «cinta: cintura»). Sin clasificar en «bien» o «mal».
- Los perímetros con cinta (cintura, brazo, muslo) son lo más sencillo y repetible para un entrenador.

### Objetivo → **Sí, pero como lista corta en vez de texto libre**

- Hoy `Client.Goal` es texto libre. Si pasa a una lista (**fuerza, masa muscular, potencia, salud y constancia**), la app puede enseñar al lado la **referencia publicada** de la nueva guía del ACSM (2026, la primera actualización desde 2009, basada en 137 revisiones sistemáticas con más de 30.000 participantes):
  - **Fuerza:** cargas en torno al 80 % del 1RM, 2–3 series por ejercicio.
  - **Masa muscular:** unas 10 series por grupo muscular y semana.
  - **Potencia:** 30–70 % del 1RM, moviendo rápido.
  - **Para todos:** cada grupo muscular al menos 2 veces por semana. La constancia importa más que la programación perfecta.
- La app ya sabe calcular **series por grupo muscular y semana** a partir de lo entrenado (nivel 2). Ponerlo al lado de «referencia ACSM: ~10» es información con fuente, no una orden. **El entrenador decide.**
- Esas cifras las he sacado de la nota de prensa del ACSM y de la cobertura en medios. **Antes de enseñarlas en la app hay que comprobarlas en el artículo original** (*Medicine & Science in Sports & Exercise*, 2026).
- Las guías son para adultos sanos en general. El propio ACSM dice que deportistas y personas muy entrenadas necesitan programas específicos. Así tiene que constar junto a la referencia.

### Días disponibles por semana → **Sí, en la ficha de cliente**

- Puramente organizativo y sin ningún riesgo.
- Hace que «Toca hoy» y «Sin noticias» del Inicio de entrenador se ajusten al ritmo de cada cliente, en vez de los 7 días fijos de ahora.
- Es lo que más mejora la experiencia del entrenador por menos esfuerzo.

### Lesiones y limitaciones → **Sí, pero solo apuntar y avisar. Es lo más valioso y lo más delicado**

- **Lo que dicen las asociaciones de entrenadores (ACE, NSCA):** el entrenador **no diagnostica ni trata** lesiones. Pregunta, deriva al profesional sanitario si hace falta y, cuando hay pautas de un médico o fisio, **entrena dentro de esos límites**.
- **Lo seguro en la app (nivel 1):**
  - Una lista de «limitaciones» por cliente: zona (hombro, rodilla, lumbar…), texto libre, fecha y, opcional, «pautas de: fisio / médico» con lo que digan.
  - Un aviso visible en la cabecera del cliente y al entrenar con él: «Felipe: hombro derecho — sin press por encima de la cabeza (fisio, 12/10)».
- **Nunca:**
  - Listas automáticas de «ejercicios contraindicados».
  - Sugerir sustituciones por lesión.
  - Interpretar síntomas.

  Eso ya es criterio clínico, y además saca a la app del terreno «bienestar» (ver Normativa).
- **Datos de salud:** en el RGPD (art. 9) son de **categoría especial**. Más en el apartado de normativa.

### Apto para entrenar (PAR-Q+ o informe médico) → **Sí, como fecha y recordatorio**

- El PAR-Q+ es el cuestionario de referencia antes de empezar a entrenar. Según su validación, solo alrededor del 1 % necesita ir al médico, y la respuesta **vale 12 meses**.
- **Propuesta:** no meter el cuestionario en la app, porque hay que reproducirlo exacto y sería mucha fricción. Basta un campo «Apto confirmado: PAR-Q+ / informe médico — fecha». Al pasar los 12 meses aparece en «Pendiente» del Inicio de entrenador: «Felipe: renovar PAR-Q+».

### Nivel o experiencia → **Solo como etiqueta, sin automatismos**

- Sirve al entrenador para orientarse, pero no hay base para que la app haga nada con él. Por ejemplo, en Zourdos et al. (2021) la experiencia no mejoraba la precisión al estimar el RIR.

### RPE / RIR (ya existe) → **Mantener, con un matiz en la ayuda**

- Estimar las repeticiones en reserva funciona razonablemente bien. Es **más preciso cerca del fallo y en series de pocas repeticiones**, y peor en series largas (Zourdos et al., 2021).
- Si algún día se muestra ayuda sobre RIR, eso es lo que debe decir.

---

## 3. Normativa (resumen para no meterse en líos)

**Producto sanitario (MDR, UE 2017/745):**
- El propio reglamento (considerando 19) dice que el software para **estilo de vida y bienestar no es producto sanitario**. La guía MDCG 2019-11 pone como ejemplo de «no sanitario» una app que registra comida o frecuencia cardiaca.
- Lo que cuenta es la **finalidad declarada**. SteelProgress se queda fuera mientras sea para **entrenar y registrar**. Entraría en terreno sanitario si diagnosticara, propusiera tratamientos o prometiera prevenir o curar lesiones.
- De ahí la regla de las lesiones: apuntar y avisar, nunca decidir. También hay que vigilar los textos de la tienda («previene lesiones» no).
- La revisión 1 de esa guía (junio de 2025) solo la he visto resumida por terceros.

**Datos de salud (RGPD art. 9 y LOPDGDD):**
- Lesiones, limitaciones, aptitud médica y probablemente el peso y la composición corporal de un cliente son **datos de salud**, de categoría especial.
- Necesitan una base específica, normalmente el **consentimiento explícito**, y el interés legítimo no sirve.
- El responsable es el **entrenador**, que es quien recoge los datos de sus clientes. La app ayuda porque todo se queda en el móvil, sin cuentas ni servidor, y porque compartir rutinas (.steel) **no exporta nada del cliente** (comprobado: `PackageExporter` solo lleva rutinas y ejercicios).
- Aun así, al añadir lesiones convendría:
  - Una casilla en la ficha: «El cliente ha dado su consentimiento para guardar estos datos».
  - Una frase en la política de privacidad.
- **Esto es un resumen hecho a partir de fuentes secundarias.** Antes de publicar en la tienda con datos de salud de clientes, conviene que lo revise alguien que sepa de protección de datos.

---

## 4. Propuesta y orden

| # | Qué | Nivel | Esfuerzo | Por qué primero |
|---|---|---|---|---|
| 1 | 1RM estimado solo con 1–10 repeticiones | 2 (corrige) | Pequeño | Arregla un número que hoy puede engañar |
| 2 | Días disponibles por cliente → «Toca hoy» y «Sin noticias» a su ritmo | 1 | Pequeño | Mucho valor para el entrenador, cero riesgo |
| 3 | Limitaciones del cliente (zona, texto, pautas de quién, fecha) + aviso al entrenar + casilla de consentimiento | 1 | Medio | Lo más valioso para un entrenador; hecho con cuidado |
| 4 | Apto (PAR-Q+ / informe) con fecha y aviso a los 12 meses en «Pendiente» | 1 | Pequeño | Buena práctica profesional, casi gratis |
| 5 | Peso corporal con media semanal o tendencia, y fuerza relativa de la persona | 1–2 | Medio | Útil en personal y en clientes |
| 6 | Objetivo como lista + series por grupo muscular y semana, con la referencia ACSM 2026 | 2 | Medio | Comprobar antes las cifras en el artículo original |
| 7 | Mediciones (perímetros, % de grasa) con método | 1 | Medio | Solo si se pide; poco uso diario |
| — | Altura, IMC, rankings entre clientes, tablas de nivel, consejos automáticos | — | — | **No**: poco fiables o fuera de lo que debe hacer la app |

## Lo que no he podido confirmar

- Las cifras exactas de la guía ACSM 2026: las he visto en la nota de prensa y en medios, no en el artículo.
- Los detalles de la revisión 1 de MDCG 2019-11, solo resumida por terceros.
- Qué campos exactos guardan Trainerize, TrueCoach o Everfit en la ficha de cliente: su documentación no salió en las búsquedas. Las plantillas de alta de cliente habituales piden peso, altura, % de grasa, perímetros, lesiones y cirugías, medicación y PAR-Q+.
- El RGPD aplicado a entrenadores en concreto: solo he encontrado fuentes generales sobre apps de fitness.

## Fuentes

**Fuerza, 1RM y RIR**
- Reynolds, Gordon y Robergs (2006). *Prediction of one repetition maximum strength from multiple repetition maximum testing and anthropometry.* J Strength Cond Res 20(3):584–592 — [texto completo](https://www.unm.edu/~rrobergs/478RMStrengthPrediction.pdf)
- LeSuer et al. (1997). J Strength Cond Res 11(4):211–213, citado en [esta revisión (arXiv)](https://arxiv.org/pdf/2603.17495v1.pdf)
- Zourdos et al. (2016). *Novel resistance training-specific RPE scale measuring repetitions in reserve* — [repositorio](https://openrepository.aut.ac.nz/items/efef3b25-6701-4fb5-bb82-55fcd2a26027/full)
- Zourdos et al. (2021). Precisión del RIR dentro de la serie — [PubMed](https://pubmed.ncbi.nlm.nih.gov/30747900/)
- Külkamp et al. (2012). *Performance comparison of resistance-trained subjects by different methods of adjusting for body mass* — [artículo](https://www.scielo.br/j/rbcdh/a/83YpqKNc9NrpDpsb487YVwQ/?lang=en)
- DOTS / Wilks / IPF GL (divulgación, no revisado) — [comparativa](https://fitnessvolt.com/rpe-training/compare/wilks-vs-dots/)

**Guías de entrenamiento**
- ACSM (2026). *Resistance Training Prescription for Muscle Function, Hypertrophy, and Physical Performance in Healthy Adults* — [ACSM](https://acsm.org/resistance-training-guidelines-update-2026/), [nota de prensa](https://www.newswise.com/articles/acsm-unveils-landmark-2026-resistance-training-guidelines-first-update-in-17-years), [ScienceDaily](https://www.sciencedaily.com/releases/2026/03/260319074552.htm)

**Peso y composición corporal**
- *The Hacker's Diet*, media móvil exponencial — [fourmilab](https://fourmilab.ch/hackdiet/www/subsubsection1_2_4_0_4_4.html)
- MacroFactor, tendencia de peso — [ayuda](https://help.macrofactorapp.com/dashboard/weight_trend/)
- Variación diaria del peso (divulgación) — [ScienceAlert](https://www.sciencealert.com/how-often-should-you-weigh-yourself-an-expert-explains)
- El IMC en deportistas — [PMC8775040](https://pmc.ncbi.nlm.nih.gov/articles/PMC8775040), [Atlas of Science](https://atlasofscience.org/bmi-is-enough-to-define-overweight-a-young-athlete/?format=pdf)
- Pliegues y cambios de masa magra — [ACU](https://acuresearchbank.acu.edu.au/item/8qyv3/validity-of-skinfold-based-measures-for-tracking-changes-in-body-composition-in-professional-rugby-league-players), [Frontiers](https://www.frontiersin.org/journals/sports-and-active-living/articles/10.3389/fspor.2023.1240252/epub)

**Cribado y ámbito del entrenador**
- PAR-Q+ (validación) — [PMC3596208](https://pmc.ncbi.nlm.nih.gov/articles/PMC3596208)
- ACE, entrenar a un cliente con lesiones crónicas — [ACE](https://www.acefitness.org/blog/6405/how-to-train-a-client-with-chronic-injuries)
- NSCA, ámbito y derivación (resumen de terceros) — [open-exam-prep](https://open-exam-prep.com/study-guides/nsca-cpt/medical-history-referral/allied-health-network)

**Normativa**
- MDCG 2019-11, qualification of software — [OpenRegulatory](https://openregulatory.com/mdcg/mdcg-2019-11), [Rev.1](https://gmpinsiders.com/mdcg-2019-11-rev1-mdr-ivdr/)
- RGPD art. 9 y datos de fitness — [Presencis](https://presencis.com/regulations/gdpr/article-9/), [Helbing, art. 4(15)](https://www.thomashelbing.com/en/wissen/dsgvo-hub/begriffe-und-definitionen/1.2.15-gesundheitsdaten)
