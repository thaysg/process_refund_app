import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../domain/models.dart';
import 'info_text.dart';

class ResultTile extends StatelessWidget {
  const ResultTile({super.key, required this.item});

  final ReimbursementItem item;

  static final NumberFormat _currency =
      NumberFormat.currency(locale: 'pt_BR', symbol: r'R$');

  String _money(num value) => _currency.format(value);

  @override
  Widget build(BuildContext context) {
    final (icon, color) = _statusVisual(context, item.statusType);
    return Semantics(
      label:
          'Solicitação ${item.id}, ${item.procedureType}, ${item.procedureDate}, pago ${_money(item.amountPaid)}, reembolsado ${_money(item.reimbursedAmount)}, cliente ${item.clientId}, status ${item.status}',
      child: Card(
        elevation: 0,
        child: Padding(
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  ExcludeSemantics(
                    child: Icon(icon, color: color),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      '#${item.id} • ${item.procedureType}',
                      style: Theme.of(context).textTheme.titleSmall,
                    ),
                  ),
                  ExcludeSemantics(
                    child: Container(
                      padding:
                          const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                      decoration: BoxDecoration(
                        color: color.withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(999),
                        border: Border.all(color: color.withValues(alpha: 0.5)),
                      ),
                      child: Text(
                        item.status,
                        style: TextStyle(color: color, fontWeight: FontWeight.w600),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Wrap(
                spacing: 12,
                runSpacing: 4,
                children: [
                  InfoText(label: 'Data', value: item.procedureDate),
                  InfoText(label: 'Cliente', value: '${item.clientId}'),
                  InfoText(label: 'Pago', value: _money(item.amountPaid)),
                  InfoText(
                      label: 'Reembolsado',
                      value: _money(item.reimbursedAmount)),
                ],
              ),
              if (item.reason != null && item.reason!.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text(
                  item.reason!,
                  style: Theme.of(context).textTheme.bodySmall,
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  (IconData, Color) _statusVisual(BuildContext context, RequestStatusType type) {
    final scheme = Theme.of(context).colorScheme;
    switch (type) {
      case RequestStatusType.aprovado:
        return (Icons.check_circle, Colors.green.shade700);
      case RequestStatusType.rejeitado:
        return (Icons.cancel, scheme.error);
      case RequestStatusType.suspeitoDeFraude:
        return (Icons.warning_amber_rounded, Colors.orange.shade800);
    }
  }
}
