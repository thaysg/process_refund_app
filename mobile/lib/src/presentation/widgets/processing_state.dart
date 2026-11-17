import 'package:flutter/material.dart';

class ProcessingStateWidget extends StatelessWidget {
  const ProcessingStateWidget({super.key});

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Padding(
        padding: EdgeInsets.symmetric(vertical: 48),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            CircularProgressIndicator(),
            SizedBox(height: 16),
            Text('Processando reembolsos...'),
          ],
        ),
      ),
    );
  }
}
