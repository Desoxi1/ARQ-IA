# Ciclos y zonas desde imagen

**Entrada:** imagen/plano con zonas, colores o números y RVT activo. **Salida:** tabla ciclo → color → nivel/vista → IDs confirmados → excluidos, más el efecto aplicado o un borrador.

1. Leer la imagen como referencia visual: leyenda, orientación, escala aparente, contorno, núcleo, accesos y vacíos. No interpretar píxeles como IDs. Si es perspectiva, pedir o localizar una planta equivalente antes de asignar muros.
2. Consultar vistas de planta y niveles reales del RVT. Comparar al menos tres hitos distintos y no colineales; considerar giro, reflejo y desplazamiento. Un nombre o una cuenta similar no demuestra coincidencia.
3. Delimitar por nivel y vista. Leer ubicación/curva de los muros candidatos y sus restricciones; identificar muros compartidos, solapes, omisiones y diferencias entre plantas repetidas. Contar cada ciclo y registrar qué queda ambiguo.
4. Distinguir color **solo en vista** (override) de pintura de cara o material del tipo. Para un pedido visual, preferir override tipado de la vista concreta. No alterar material compartido por inferencia.
5. Cambiar únicamente IDs cuya correspondencia quedó confirmada. Tras cada lote, consultar la vista y los elementos afectados y comparar color, cantidad, ciclo y nivel con la tabla previa. Si la lectura falla, reportar resultado no verificado.

Si faltan hitos suficientes, excluir la zona dudosa y solicitar **una** planta/cota/referencia concreta; continuar con las zonas independientes. Reutilizar una rutina verificada, sin regenerar un grafo por cada planta.

Fuentes: [vistas y niveles de Revit](https://help.autodesk.com/view/RVT/2027/ENU/), [formación oficial de Dynamo](https://www.autodesk.com/support/technical/article/caas/sfdcarticles/sfdcarticles/How-to-Training-for-Dynamo.html).
