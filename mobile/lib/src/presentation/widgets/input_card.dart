import 'package:flutter/material.dart';

class InputCard extends StatelessWidget {
  const InputCard({
    super.key,
    required this.fileName,
    required this.referenceDateLabel,
    required this.loading,
    required this.onPickFile,
    required this.onPickDate,
    required this.onProcess,
  });

  final String? fileName;
  final String referenceDateLabel;
  final bool loading;
  final VoidCallback onPickFile;
  final VoidCallback onPickDate;
  final VoidCallback onProcess;

  @override
  Widget build(BuildContext context) {
    return Card(
      elevation: 0,
      clipBehavior: Clip.antiAlias,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Entrada', style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 12),
            Semantics(
              label: fileName == null
                  ? 'Nenhum arquivo CSV selecionado. Toque para escolher um arquivo.'
                  : 'Arquivo CSV selecionado: $fileName. Toque para escolher outro arquivo.',
              button: true,
              child: OutlinedButton.icon(
                onPressed: loading ? null : onPickFile,
                icon: const Icon(Icons.attach_file),
                label: Text(
                  fileName ?? 'Escolher arquivo CSV',
                  overflow: TextOverflow.ellipsis,
                ),
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size.fromHeight(48),
                  alignment: Alignment.centerLeft,
                ),
              ),
            ),
            const SizedBox(height: 12),
            Semantics(
              label: 'Data de referência $referenceDateLabel. Toque para alterar.',
              button: true,
              child: OutlinedButton.icon(
                onPressed: loading ? null : onPickDate,
                icon: const Icon(Icons.calendar_today),
                label: Text('Data de referência: $referenceDateLabel'),
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size.fromHeight(48),
                  alignment: Alignment.centerLeft,
                ),
              ),
            ),
            const SizedBox(height: 16),
            FilledButton.icon(
              onPressed: loading ? null : onProcess,
              icon: loading
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Icon(Icons.play_arrow),
              label: Text(loading ? 'Processando...' : 'Processar'),
              style:
                  FilledButton.styleFrom(minimumSize: const Size.fromHeight(48)),
            ),
          ],
        ),
      ),
    );
  }
}
