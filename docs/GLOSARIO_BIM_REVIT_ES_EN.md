# Glosario inicial BIM/Revit español ↔ inglés

Este vocabulario ayuda a Codex a traducir entre lenguaje de oficina y términos técnicos. Sirve como guía de conversación; la categoría, tipo, parámetro y valor reales se deben consultar en el modelo abierto.

| Lenguaje habitual | Término técnico Revit/API | Qué debe distinguir el asistente |
|---|---|---|
| Nivel, piso del edificio | Level | Elevación, nombre y nivel de restricción de los elementos. Un nivel del modelo no es lo mismo que una planta/vista. |
| Eje, grilla | Grid | Línea de referencia y nombre; confirmar dirección, separación y extensiones. |
| Muro, tabique | Wall / WallType | Instancia frente a tipo; ubicación, nivel, altura, espesor y restricciones. |
| Pintar, colorear, poner rojo | View-specific graphic override / Material assignment | Un color de presentación en una vista usa `OverrideGraphicSettings`; cambiar material afecta la apariencia del elemento en otras vistas. Preguntar solo si la intención no queda clara tras leer el contexto. |
| Material, acabado, revestimiento | `Material`, material de capa, parámetro de material o pintura de cara | Buscar primero el material en el RVT activo. El nombre, clase, aspecto de render, gráficos, propiedades térmicas y físicas son datos distintos; no inferir especificación por el nombre. |
| Cambiar textura o color del material | `AppearanceAsset`, propiedades gráficas o override de vista | Identificar qué representación debe cambiar y si el material es compartido por otros elementos. Una textura de render no equivale al color de sombreado ni al patrón de corte. |
| Cubicar material | Material takeoff / cantidades de material | Distinguir materiales definidos, materiales usados y área/volumen cuantificado. Confirmar categoría, fase, unidades y alcance. |
| Muros del primer piso | Wall instances con nivel base / vista de planta | Confirmar el nivel mediante `Base Constraint` o detalles del elemento; una vista llamada “Piso 1” no demuestra que todos sus muros pertenezcan a ese nivel. |
| Piso, losa | Floor / FloorType | El término local puede referirse a piso arquitectónico o losa estructural; confirmar tipo y contorno. |
| Habitación, recinto | Room | Espacio colocado en un nivel y delimitado por elementos separadores; confirmar punto, nombre y número. |
| Puerta/ventana/familia | Family / FamilySymbol / FamilyInstance | Familia es definición; tipo/símbolo es variante; instancia es el objeto colocado. Confirmar tipo y anfitrión para elementos hospedados. |
| Tipo, modelo de puerta/muro | ElementType / FamilySymbol | Cambiar un tipo puede afectar todas sus instancias; diferenciar de cambiar solo un elemento. |
| Parámetro de elemento | Instance parameter | Afecta una instancia concreta; identificar elemento, parámetro y tipo de dato. |
| Parámetro de tipo | Type parameter | Puede afectar muchas instancias que comparten el tipo; hacer explícito el alcance. |
| Anfitrión, muro anfitrión | Host element | La puerta/ventana debe llevar `host_id` correcto; el puente no debe inferir un anfitrión equivocado. |
| Planta, vista, lámina | View / ViewPlan / ViewSheet | Una vista muestra modelo; una lámina organiza vistas. Confirmar cuál se consulta o modifica. |
| Cota, dimensión | Dimension | Requiere referencias geométricas válidas y vista compatible; no basta con dos IDs si faltan referencias. |
| Etiqueta, tag | IndependentTag | Anota una instancia en una vista; confirmar vista, tipo de etiqueta y orientación. |
| Comentarios | Comments parameter | Confirmar si se cambia en instancia o tipo y qué elementos se incluyen. |
| Altura/elevación | Height / Elevation | Aclarar altura libre, altura de muro o elevación absoluta; verificar unidades. |
| Seleccionados | Revit UI Selection / ElementIds | Usar IDs devueltos por la consulta actual; selección vacía o caducada requiere volver a consultar. |
| mm, centímetros, metros | Unidades de entrada | Algunas herramientas MCP reciben coordenadas y longitudes en milímetros; Revit API usa unidades internas en varias operaciones. Convertir solo cuando la herramienta lo indique. |
| Restricción inferior/superior, desfase | Base Constraint / Top Constraint, Base Offset / Top Offset | Confirmar nivel de referencia, altura medida desde el nivel o elevación absoluta; no tratar `offset` como altura total. |
| Fase nueva, existente o demolición | Phase Created / Phase Demolished | Distinguir crear una fase del proyecto de asignar un elemento a una fase; consultar fases existentes y vista/filtro activo. |
| Opción de diseño | Design Option / Option Set | No confundir una opción de diseño con tipo de familia, grupo o fase; confirmar conjunto y opción primaria/secundaria. |
| Subproyecto | Workset | Es una división de trabajo colaborativo; no es nivel, fase ni categoría. La asignación puede afectar permisos y colaboración. |
| Grupo, repetir como grupo | Model Group / Group Instance | Distinguir editar una instancia del grupo de cambiar la definición compartida por todas sus instancias. |
| Plantilla de vista | View Template | Controla propiedades de una o varias vistas; confirmar si se cambia la vista o la plantilla que puede afectar muchas vistas. |
| Caja de referencia | Scope Box | Delimita extensiones de vistas; confirmar qué vistas reciben la caja y si se modifica el elemento o solo su presentación. |
| Plano de trabajo | Work Plane | Referencia de colocación/orientación para geometría o familias; no equivale al nivel ni a una cara anfitriona. |
| Anfitrión / alojado | Host / Hosted Family | Una familia hospedada depende de un muro, techo, cara u otro soporte; consultar `host_id` y compatibilidad del tipo. |
| Referencia geométrica | Geometry Reference | Se requiere para cotas y algunas restricciones; una cara visible o un `ElementId` no siempre constituye una referencia válida. |
| Alargar / subir / mover | Extend / Raise / Move | Aclarar si cambia longitud, altura, elevación, ubicación o restricción antes de escoger herramienta y parámetro. |
| Instancia / todos los del tipo | Instance / All instances of type | Preguntar el alcance. Un cambio en un parámetro de tipo puede modificar muchos elementos a la vez. |

## Regla para ampliar el glosario

Cada entrada nueva debe incluir: frase original usada por el arquitecto, término técnico confirmado, posibles interpretaciones, datos que faltan, herramienta o consulta que lo verifica, unidades y un caso probado. Si una frase admite más de una interpretación material, Codex pregunta antes de escribir. Las frases de prueba y las respuestas verificadas se registran en `tests/` para mantener equivalencia entre lenguaje cotidiano y técnico; una conversación sin revisión no se promueve automáticamente como conocimiento aprobado.
