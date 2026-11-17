enum RequestStatusType { aprovado, rejeitado, suspeitoDeFraude }

RequestStatusType? statusFromApi(String value) {
  switch (value) {
    case 'Aprovado':
      return RequestStatusType.aprovado;
    case 'Rejeitado':
      return RequestStatusType.rejeitado;
    case 'Suspeito de Fraude':
      return RequestStatusType.suspeitoDeFraude;
    default:
      return null;
  }
}

String statusLabel(RequestStatusType status) {
  switch (status) {
    case RequestStatusType.aprovado:
      return 'Aprovado';
    case RequestStatusType.rejeitado:
      return 'Rejeitado';
    case RequestStatusType.suspeitoDeFraude:
      return 'Suspeito de Fraude';
  }
}

class ReimbursementItem {
  final int id;
  final String procedureType;
  final String procedureDate;
  final num amountPaid;
  final num reimbursedAmount;
  final int clientId;
  final String status;
  final RequestStatusType statusType;
  final String? reason;
  final String? reasonCode;

  const ReimbursementItem({
    required this.id,
    required this.procedureType,
    required this.procedureDate,
    required this.amountPaid,
    required this.reimbursedAmount,
    required this.clientId,
    required this.status,
    required this.statusType,
    this.reason,
    this.reasonCode,
  });

  factory ReimbursementItem.fromJson(Map<String, dynamic> json) {
    final statusStr = json['status'] as String? ?? '';
    final parsedStatus = statusFromApi(statusStr);
    if (parsedStatus == null) {
      throw const FormatException('Status desconhecido recebido da API.');
    }
    return ReimbursementItem(
      id: (json['id'] as num).toInt(),
      procedureType: json['procedureType'] as String? ?? '',
      procedureDate: json['procedureDate'] as String? ?? '',
      amountPaid: json['amountPaid'] as num? ?? 0,
      reimbursedAmount: json['reimbursedAmount'] as num? ?? 0,
      clientId: (json['clientId'] as num).toInt(),
      status: statusStr,
      statusType: parsedStatus,
      reason: json['reason'] as String?,
      reasonCode: json['reasonCode'] as String?,
    );
  }
}

class ProcessingSummary {
  final int total;
  final int approved;
  final int rejected;
  final int suspectedFraud;
  final num totalReimbursed;

  const ProcessingSummary({
    required this.total,
    required this.approved,
    required this.rejected,
    required this.suspectedFraud,
    required this.totalReimbursed,
  });

  factory ProcessingSummary.fromJson(Map<String, dynamic> json) {
    return ProcessingSummary(
      total: (json['total'] as num?)?.toInt() ?? 0,
      approved: (json['approved'] as num?)?.toInt() ?? 0,
      rejected: (json['rejected'] as num?)?.toInt() ?? 0,
      suspectedFraud: (json['suspectedFraud'] as num?)?.toInt() ?? 0,
      totalReimbursed: json['totalReimbursed'] as num? ?? 0,
    );
  }
}

class ProcessingResponse {
  final String referenceDate;
  final ProcessingSummary summary;
  final List<ReimbursementItem> items;

  const ProcessingResponse({
    required this.referenceDate,
    required this.summary,
    required this.items,
  });

  factory ProcessingResponse.fromJson(Map<String, dynamic> json) {
    final itemsJson = (json['items'] as List<dynamic>? ?? []);
    return ProcessingResponse(
      referenceDate: json['referenceDate'] as String? ?? '',
      summary: ProcessingSummary.fromJson(
        json['summary'] as Map<String, dynamic>? ?? const {},
      ),
      items: itemsJson
          .map((e) => ReimbursementItem.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }
}
