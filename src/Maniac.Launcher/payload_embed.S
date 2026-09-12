#ifdef __APPLE__
.section __DATA,__const
.globl _maniac_payload
.globl _maniac_payload_end
_maniac_payload:
    .incbin "payload.bin"
_maniac_payload_end:

.p2align 2
.globl _maniac_payload_size
_maniac_payload_size:
    .long _maniac_payload_end - _maniac_payload
#else
.section .rodata
.globl maniac_payload
.globl maniac_payload_end
maniac_payload:
    .incbin "payload.bin"
maniac_payload_end:

.section .rodata
.align 4
.globl maniac_payload_size
maniac_payload_size:
    .long maniac_payload_end - maniac_payload
#endif
