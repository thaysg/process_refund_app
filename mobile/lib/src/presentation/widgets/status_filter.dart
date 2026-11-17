import 'package:flutter/material.dart';

import '../../domain/models.dart';

class StatusFilter extends StatelessWidget {
  const StatusFilter({
    super.key,
    required this.selected,
    required this.onSelected,
  });

  final RequestStatusType? selected;
  final ValueChanged<RequestStatusType?> onSelected;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('Filtrar por status',
            style: Theme.of(context).textTheme.titleSmall),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            _chip(context, 'Todos', null),
            _chip(context, 'Aprovado', RequestStatusType.aprovado),
            _chip(context, 'Rejeitado', RequestStatusType.rejeitado),
            _chip(context, 'Suspeito de Fraude',
                RequestStatusType.suspeitoDeFraude),
          ],
        ),
      ],
    );
  }

  Widget _chip(BuildContext context, String label, RequestStatusType? type) {
    final isSelected = selected == type;
    return Semantics(
      label: 'Filtro $label',
      selected: isSelected,
      button: true,
      child: FilterChip(
        label: Text(label),
        selected: isSelected,
        onSelected: (_) => onSelected(type),
        materialTapTargetSize: MaterialTapTargetSize.padded,
      ),
    );
  }
}
