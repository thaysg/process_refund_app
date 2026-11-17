import 'package:flutter/material.dart';
import 'package:intl/intl.dart';

import '../../domain/models.dart';
import 'summary_chip.dart';

class SummaryHeader extends StatelessWidget {
  const SummaryHeader({
    super.key,
    required this.summary,
    required this.referenceDate,
  });

  final ProcessingSummary summary;
  final String referenceDate;

  static final NumberFormat _currency =
      NumberFormat.currency(locale: 'pt_BR', symbol: r'R$');

  @override
  Widget build(BuildContext context) {
    final scheme = Theme.of(context).colorScheme;
    final totalReimbursed = _currency.format(summary.totalReimbursed);
    return Card(
      elevation: 0,
      color: scheme.surfaceContainerHighest,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Resumo (ref: $referenceDate)',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                SummaryChip(
                  label: 'Total',
                  value: '${summary.total}',
                  icon: Icons.list_alt,
                ),
                SummaryChip(
                  label: 'Aprovado',
                  value: '${summary.approved}',
                  icon: Icons.check_circle_outline,
                ),
                SummaryChip(
                  label: 'Rejeitado',
                  value: '${summary.rejected}',
                  icon: Icons.cancel_outlined,
                ),
                SummaryChip(
                  label: 'Suspeito de Fraude',
                  value: '${summary.suspectedFraud}',
                  icon: Icons.warning_amber_outlined,
                ),
              ],
            ),
            const SizedBox(height: 12),
            Semantics(
              label: 'Total reembolsado $totalReimbursed',
              child: ExcludeSemantics(
                child: Text(
                  'Total reembolsado (aprovados): $totalReimbursed',
                  style: Theme.of(context)
                      .textTheme
                      .bodyLarge
                      ?.copyWith(fontWeight: FontWeight.w600),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
