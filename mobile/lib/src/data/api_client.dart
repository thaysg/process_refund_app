import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:http/http.dart' as http;

import '../domain/models.dart';

class ApiException implements Exception {
  final String message;
  final int? statusCode;
  final Map<String, dynamic>? details;

  ApiException(this.message, {this.statusCode, this.details});

  @override
  String toString() => message;
}

class ReimbursementApiClient {
  ReimbursementApiClient({
    http.Client? httpClient,
    String? baseUrl,
    Duration timeout = const Duration(seconds: 30),
  })  : _http = httpClient ?? http.Client(),
        _baseUrl = baseUrl ??
            const String.fromEnvironment(
              'API_BASE_URL',
              defaultValue: 'http://10.0.2.2:5000',
            ),
        _timeout = timeout;

  final http.Client _http;
  final String _baseUrl;
  final Duration _timeout;

  String get baseUrl => _baseUrl;

  Uri _uri(String path) {
    final normalizedBase = _baseUrl.endsWith('/')
        ? _baseUrl.substring(0, _baseUrl.length - 1)
        : _baseUrl;
    return Uri.parse('$normalizedBase$path');
  }

  Future<ProcessingResponse> processCsv({
    required Uint8List fileBytes,
    required String fileName,
    required String referenceDate,
  }) async {
    try {
      final request = http.MultipartRequest(
        'POST',
        _uri('/api/reimbursements/process'),
      );
      request.fields['referenceDate'] = referenceDate;
      request.files.add(http.MultipartFile.fromBytes(
        'file',
        fileBytes,
        filename: fileName,
      ));

      final streamed = await _http.send(request).timeout(_timeout);
      final response = await http.Response.fromStream(streamed).timeout(_timeout);
      return _handleResponse(response);
    } on TimeoutException {
      throw ApiException(
          'A API demorou demais para responder. Verifique a conexão e a URL base.');
    } on FormatException catch (e) {
      throw ApiException(_withDebugBaseUrl(e.message));
    } on SocketException {
      throw ApiException(_withDebugBaseUrl(
          'Não foi possível conectar à API. Verifique se o servidor está em execução e se a URL base da API está correta.'));
    } on http.ClientException {
      throw ApiException(_withDebugBaseUrl(
          'Não foi possível conectar à API. Verifique se o servidor está em execução e se a URL base da API está correta.'));
    }
  }

  String _withDebugBaseUrl(String message) {
    if (kDebugMode) {
      return '$message (URL base: $_baseUrl)';
    }
    return message;
  }

  ProcessingResponse _handleResponse(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      final decoded = json.decode(utf8.decode(response.bodyBytes));
      if (decoded is! Map<String, dynamic>) {
        throw ApiException('Resposta inválida da API.');
      }
      try {
        return ProcessingResponse.fromJson(decoded);
      } on FormatException catch (e) {
        throw ApiException(e.message);
      }
    }
    throw _mapError(response);
  }

  ApiException _mapError(http.Response response) {
    try {
      final decoded = json.decode(utf8.decode(response.bodyBytes));
      if (decoded is Map<String, dynamic>) {
        final errors = decoded['errors'];
        if (errors is List && errors.isNotEmpty) {
          final parts = <String>[];
          for (final e in errors) {
            if (e is Map) {
              final line = e['line'];
              final field = e['field'];
              final message = e['message'];
              final prefix = line != null ? 'Linha $line' : 'Linha';
              final fieldPart =
                  (field is String && field.isNotEmpty) ? ' [$field]' : '';
              parts.add('$prefix$fieldPart: ${message ?? ''}');
            }
          }
          if (parts.isNotEmpty) {
            return ApiException(parts.join('\n'),
                statusCode: response.statusCode, details: decoded);
          }
        }

        final title = decoded['title'] as String?;
        final detail = decoded['detail'] as String?;
        final fieldErrors = decoded['errors'];
        if (fieldErrors is Map) {
          final parts = <String>[];
          fieldErrors.forEach((key, value) {
            if (value is List) {
              parts.addAll(value.map((e) => e.toString()));
            } else if (value != null) {
              parts.add(value.toString());
            }
          });
          if (parts.isNotEmpty) {
            return ApiException(parts.join('\n'),
                statusCode: response.statusCode, details: decoded);
          }
        }

        final message = detail ?? title ?? 'Falha na requisição.';
        return ApiException(message,
            statusCode: response.statusCode, details: decoded);
      }
    } catch (_) {
      // fall through
    }
    return ApiException(
      'Falha na requisição (status ${response.statusCode}).',
      statusCode: response.statusCode,
    );
  }
}
